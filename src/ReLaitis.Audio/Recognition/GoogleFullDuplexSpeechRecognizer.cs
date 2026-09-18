#pragma warning disable SYSLIB0014 // WebRequest used for chunked upstream streaming compatibility

using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;

namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Потоковый облачный распознаватель речи Google Full-Duplex v1 (протокол Chromium).
/// Использует одновременный upstream POST для передачи 16kHz PCM аудио и downstream GET для чтения транскрипций.
/// </summary>
public class GoogleFullDuplexSpeechRecognizer : ISpeechRecognizer
{
    private const string BaseUrl = "https://www.google.com/speech-api/full-duplex/v1/";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public event EventHandler<string>? PartialResultRecognized;
    public event EventHandler<string>? FinalResultRecognized;

    public bool IsModelLoaded => true;
    public string Language { get; set; } = "ru-RU";

    private readonly object _lock = new();
    private Channel<byte[]>? _audioChannel;
    private CancellationTokenSource? _sessionCts;
    private Task? _upstreamTask;
    private Task? _downstreamTask;
    private bool _isSessionActive;

    public bool LoadModel(string modelPath) => true;

    public void ProcessAudio(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        lock (_lock)
        {
            EnsureSessionStarted();
            _audioChannel?.Writer.TryWrite(data);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            StopSession();
        }
    }

    private void EnsureSessionStarted()
    {
        if (_isSessionActive && _sessionCts != null && !_sessionCts.IsCancellationRequested)
            return;

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();
        var token = _sessionCts.Token;

        _audioChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var pairId = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        _isSessionActive = true;

        _upstreamTask = Task.Run(() => RunUpstreamAsync(pairId, token), token);
        _downstreamTask = Task.Run(() => RunDownstreamAsync(pairId, token), token);
    }

    private void StopSession()
    {
        _isSessionActive = false;
        try
        {
            _audioChannel?.Writer.TryComplete();
            _sessionCts?.Cancel();
        }
        catch { }
    }

    private async Task RunUpstreamAsync(string pairId, CancellationToken ct)
    {
        HttpWebRequest? request = null;
        Stream? requestStream = null;
        try
        {
            var url = $"{BaseUrl}up?pair={pairId}&lang={Language}&client=chromium";
            request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.UserAgent = UserAgent;
            request.ContentType = "audio/l16; rate=16000";
            request.SendChunked = true;
            request.KeepAlive = true;
            request.Timeout = 15000;

            requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false);

            if (_audioChannel != null)
            {
                var reader = _audioChannel.Reader;
                while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var chunk))
                    {
                        if (ct.IsCancellationRequested) break;
                        await requestStream.WriteAsync(chunk, 0, chunk.Length, ct).ConfigureAwait(false);
                        await requestStream.FlushAsync(ct).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            try { requestStream?.Dispose(); } catch { }
            try { request?.Abort(); } catch { }
        }
    }

    private async Task RunDownstreamAsync(string pairId, CancellationToken ct)
    {
        HttpWebRequest? request = null;
        WebResponse? response = null;
        StreamReader? reader = null;
        try
        {
            var url = $"{BaseUrl}down?pair={pairId}&client=chromium";
            request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.KeepAlive = true;
            request.Timeout = 30000;

            response = await request.GetResponseAsync().ConfigureAwait(false);
            var responseStream = response.GetResponseStream();
            if (responseStream == null) return;

            reader = new StreamReader(responseStream);
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line)) continue;

                ParseChunk(line);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            try { reader?.Dispose(); } catch { }
            try { response?.Dispose(); } catch { }
            try { request?.Abort(); } catch { }
        }
    }

    private void ParseChunk(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("result", out var resultElem) ||
                resultElem.ValueKind != JsonValueKind.Array ||
                resultElem.GetArrayLength() == 0)
            {
                return;
            }

            var firstResult = resultElem[0];
            var isFinal = firstResult.TryGetProperty("final", out var finalElem) && finalElem.GetBoolean();

            if (firstResult.TryGetProperty("alternative", out var altElem) &&
                altElem.ValueKind == JsonValueKind.Array &&
                altElem.GetArrayLength() > 0)
            {
                var firstAlt = altElem[0];
                if (firstAlt.TryGetProperty("transcript", out var transcriptElem))
                {
                    var text = transcriptElem.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var trimmed = text.Trim();
                        if (isFinal)
                        {
                            FinalResultRecognized?.Invoke(this, trimmed);
                            Reset();
                        }
                        else
                        {
                            PartialResultRecognized?.Invoke(this, trimmed);
                        }
                    }
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        StopSession();
        _sessionCts?.Dispose();
    }
}
