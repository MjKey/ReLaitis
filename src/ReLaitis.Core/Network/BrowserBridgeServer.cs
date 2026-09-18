using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReLaitis.Core.Interfaces;

namespace ReLaitis.Core.Network;

/// <summary>
/// Локальный встраиваемый WebSocket сервер для связи ReLaitis с браузерным расширением.
/// Работает на http://127.0.0.1:11337/
/// </summary>
public class BrowserBridgeServer : IBrowserBridge, IDisposable
{
    private readonly int _port;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerLoopTask;

    private readonly object _clientLock = new();
    private WebSocket? _activeClient;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<BridgeResponse>> _pendingRequests = new();

    public bool IsConnected
    {
        get
        {
            lock (_clientLock)
            {
                return _activeClient != null && _activeClient.State == WebSocketState.Open;
            }
        }
    }

    public string? CurrentUrl { get; private set; }
    public string? CurrentTitle { get; private set; }

    public event Action<bool>? ConnectionChanged;
    public event Action<string, string>? PageStateChanged;
    public event Action<string>? LogMessage;

    public BrowserBridgeServer(int port = 11337)
    {
        _port = port;
    }

    public void Start()
    {
        if (_listener != null) return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            _listenerLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
            LogMessage?.Invoke($"WebSocket сервер запущен на 127.0.0.1:{_port}");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Не удалось запустить WebSocket сервер на порту {_port}: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;

            lock (_clientLock)
            {
                if (_activeClient != null)
                {
                    try { _activeClient.Dispose(); } catch { }
                    _activeClient = null;
                }
            }

            foreach (var pending in _pendingRequests.Values)
            {
                pending.TrySetCanceled();
            }
            _pendingRequests.Clear();

            ConnectionChanged?.Invoke(false);
            LogMessage?.Invoke("WebSocket сервер остановлен");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка при остановке сервера: {ex.Message}");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketConnectionAsync(context, ct);
                }
                else
                {
                    await HandleHttpRequestAsync(context);
                }
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) break;
                LogMessage?.Invoke($"Ошибка цикла приема подключений: {ex.Message}");
            }
        }
    }

    private async Task HandleHttpRequestAsync(HttpListenerContext context)
    {
        try
        {
            // Настройка CORS для обращения из расширения и веб-страниц
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "*");

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
                return;
            }

            var path = context.Request.Url?.AbsolutePath.ToLowerInvariant() ?? "";
            if (path is "/status" or "/health" or "/")
            {
                var data = new
                {
                    status = "ok",
                    service = "ReLaitis Voice Bridge",
                    isConnected = IsConnected,
                    currentUrl = CurrentUrl,
                    currentTitle = CurrentTitle
                };

                var json = JsonSerializer.Serialize(data);
                var buffer = Encoding.UTF8.GetBytes(json);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await context.Response.OutputStream.WriteAsync(buffer);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
        catch
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private async Task HandleWebSocketConnectionAsync(HttpListenerContext context, CancellationToken ct)
    {
        WebSocketContext? wsContext = null;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка подтверждения WebSocket соединения: {ex.Message}");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
            catch { }
            return;
        }

        var webSocket = wsContext.WebSocket;

        lock (_clientLock)
        {
            if (_activeClient != null)
            {
                try { _activeClient.Abort(); } catch { }
                try { _activeClient.Dispose(); } catch { }
            }
            _activeClient = webSocket;
        }

        ConnectionChanged?.Invoke(true);
        LogMessage?.Invoke("Расширение браузера ReLaitis подключено");

        var buffer = new byte[16 * 1024];

        try
        {
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var messageJson = Encoding.UTF8.GetString(ms.ToArray());
                ProcessIncomingMessage(messageJson);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMessage?.Invoke($"WebSocket клиент отключен: {ex.Message}");
        }
        finally
        {
            lock (_clientLock)
            {
                if (ReferenceEquals(_activeClient, webSocket))
                {
                    _activeClient = null;
                }
            }

            try { webSocket.Dispose(); } catch { }

            ConnectionChanged?.Invoke(false);
            LogMessage?.Invoke("Расширение браузера ReLaitis отключено");
        }
    }

    private void ProcessIncomingMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1. Проверяем, является ли сообщение ответом на RPC запрос
            if (root.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                if (!string.IsNullOrEmpty(id) && _pendingRequests.TryRemove(id, out var tcs))
                {
                    var success = root.TryGetProperty("success", out var succProp) && succProp.GetBoolean();
                    var data = root.TryGetProperty("data", out var dataProp) ? dataProp.GetString() : null;
                    var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                    tcs.TrySetResult(new BridgeResponse(success, data, error));
                    return;
                }
            }

            // 2. Проверяем системные события (обновление состояния вкладки)
            if (root.TryGetProperty("type", out var typeProp))
            {
                var type = typeProp.GetString();
                if (type == "state_changed")
                {
                    var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
                    var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;

                    CurrentUrl = url;
                    CurrentTitle = title;
                    PageStateChanged?.Invoke(url ?? "", title ?? "");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Ошибка разбора сообщения WebSocket: {ex.Message}");
        }
    }

    public async Task<bool> ClickElementAsync(string target, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "click", target }, ct);
        return resp.Success;
    }

    public async Task<bool> NavigateAsync(string url, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "navigate", url }, ct);
        return resp.Success;
    }

    public async Task<bool> ScrollAsync(string direction, int amount = 400, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "scroll", direction, amount }, ct);
        return resp.Success;
    }

    public async Task<bool> ExecuteScriptAsync(string code, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "script", code }, ct);
        return resp.Success;
    }

    public async Task<bool> ToggleHintsAsync(bool show, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "toggle_hints", show }, ct);
        return resp.Success;
    }

    public async Task<string?> GetElementTextAsync(string selector, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "get_text", selector }, ct);
        return resp.Success ? resp.Data : null;
    }

    public async Task<bool> TabActionAsync(string command, CancellationToken ct = default)
    {
        var resp = await SendCommandAsync(new { action = "tab_action", command }, ct);
        return resp.Success;
    }

    private async Task<BridgeResponse> SendCommandAsync(object payload, CancellationToken ct)
    {
        WebSocket? client;
        lock (_clientLock)
        {
            client = _activeClient;
        }

        if (client == null || client.State != WebSocketState.Open)
        {
            return new BridgeResponse(false, null, "Browser extension is not connected");
        }

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<BridgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        // Создаем общий словарь с добавлением id
        var dict = new Dictionary<string, object>();
        dict["id"] = id;
        foreach (var prop in payload.GetType().GetProperties())
        {
            dict[prop.Name] = prop.GetValue(payload)!;
        }

        var json = JsonSerializer.Serialize(dict);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            if (client.State != WebSocketState.Open)
            {
                _pendingRequests.TryRemove(id, out _);
                return new BridgeResponse(false, null, "WebSocket connection was closed");
            }

            await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
        catch (Exception ex)
        {
            _pendingRequests.TryRemove(id, out _);
            return new BridgeResponse(false, null, ex.Message);
        }
        finally
        {
            _sendLock.Release();
        }

        // Ожидание ответа с таймаутом (5 секунд)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.TryRemove(id, out _);
            if (timeoutCts.IsCancellationRequested)
                return new BridgeResponse(false, null, "Request timed out waiting for browser extension");

            return new BridgeResponse(false, null, "Request canceled");
        }
        catch (Exception ex)
        {
            _pendingRequests.TryRemove(id, out _);
            return new BridgeResponse(false, null, ex.Message);
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _sendLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private record BridgeResponse(bool Success, string? Data, string? Error);
}
