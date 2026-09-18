using System.Net.WebSockets;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using NAudio.Wave;
using ReLaitis.Core.Interfaces;

namespace ReLaitis.Audio.Feedback;

/// <summary>
/// Бесплатный студийный синтезатор нейросетевой русской речи на базе Microsoft Edge Neural TTS.
/// Не требует API ключей, обеспечивает высочайшее качество озвучки (Светлана, Дмитрий).
/// </summary>
public class EdgeNeuralTtsService : IVoiceFeedback, IDisposable
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string SecMsGecVersion = "1-143.0.3650.75";
    private WaveOut? _currentWaveOut;
    private CancellationTokenSource? _activeCts;

    public string CurrentVoice { get; set; } = "ru-RU-SvetlanaNeural";
    public int SpeechRate { get; set; } = 0; // -10 .. +10

    public static string GenerateSecMsGec()
    {
        // Seconds since Jan 1, 1601 UTC (Windows Epoch)
        var unixSeconds = (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ticks = unixSeconds + 11644473600.0;
        ticks -= ticks % 300.0;
        ticks *= 10_000_000.0;
        var toHash = $"{ticks:0}{TrustedClientToken}";
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(toHash));
        return Convert.ToHexString(hash);
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        StopSpeaking();
        _activeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _activeCts.Token;

        try
        {
            var mp3Bytes = await DownloadAudioAsync(text, CurrentVoice, SpeechRate, token);
            if (mp3Bytes.Length == 0 || token.IsCancellationRequested)
                return;

            await PlayMp3StreamAsync(mp3Bytes, token);
        }
        catch (OperationCanceledException)
        {
            // Отмена воспроизведения
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EdgeTTS] Ошибка: {ex.Message}");
            throw;
        }
    }

    private static async Task<byte[]> DownloadAudioAsync(string text, string voice, int rate, CancellationToken token)
    {
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
        ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        ws.Options.SetRequestHeader("Pragma", "no-cache");
        ws.Options.SetRequestHeader("Cache-Control", "no-cache");
        ws.Options.SetRequestHeader("Accept-Language", "ru,en-US,en;q=0.9");
        var muid = Guid.NewGuid().ToString("N").ToUpperInvariant();
        ws.Options.SetRequestHeader("Cookie", $"muid={muid};");

        var secMsGec = GenerateSecMsGec();
        var connUrl = $"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken={TrustedClientToken}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={SecMsGecVersion}&ConnectionId={Guid.NewGuid():N}";
        await ws.ConnectAsync(new Uri(connUrl), token);

        // 1. Отправляем конфигурацию формата аудио (MP3 24kHz)
        var configMessage = "Content-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";
        var configBytes = Encoding.UTF8.GetBytes(configMessage);
        await ws.SendAsync(new ArraySegment<byte>(configBytes), WebSocketMessageType.Text, true, token);

        // 2. Формируем SSML запрос
        var requestId = Guid.NewGuid().ToString("N");
        var ssml = BuildSsml(text, voice, rate);

        var ssmlMessage = $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nPath:ssml\r\n\r\n{ssml}";
        var ssmlBytes = Encoding.UTF8.GetBytes(ssmlMessage);
        await ws.SendAsync(new ArraySegment<byte>(ssmlBytes), WebSocketMessageType.Text, true, token);

        // 3. Получаем ответные аудио-фреймы
        using var audioStream = new MemoryStream();
        var buffer = new byte[8192];

        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var textChunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (textChunk.Contains("Path:turn.end"))
                    break;
            }
            else if (result.MessageType == WebSocketMessageType.Binary && result.Count > 2)
            {
                // Заголовок бинарного сообщения: первые 2 байта (Big Endian) содержат длину текстового заголовка
                var headerLength = (buffer[0] << 8) | buffer[1];
                var dataOffset = 2 + headerLength;

                if (result.Count > dataOffset)
                {
                    audioStream.Write(buffer, dataOffset, result.Count - dataOffset);
                }
            }
        }

        try
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Complete", CancellationToken.None);
        }
        catch { }

        return audioStream.ToArray();
    }

    private async Task PlayMp3StreamAsync(byte[] mp3Data, CancellationToken token)
    {
        var tcs = new TaskCompletionSource();
        using var memStream = new MemoryStream(mp3Data);
        using var mp3Reader = new Mp3FileReader(memStream);

        var waveOut = new WaveOut();
        _currentWaveOut = waveOut;
        try
        {
            waveOut.Init(mp3Reader);
            waveOut.PlaybackStopped += (s, e) => tcs.TrySetResult();
            waveOut.Play();

            using (token.Register(() =>
            {
                try { waveOut.Stop(); } catch { }
                tcs.TrySetCanceled();
            }))
            {
                await tcs.Task;
            }
        }
        finally
        {
            try
            {
                waveOut.Stop();
                waveOut.Dispose();
            }
            catch { }
            if (ReferenceEquals(_currentWaveOut, waveOut))
                _currentWaveOut = null;
        }
    }

    public async Task PlayAudioAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            return;

        try
        {
            StopSpeaking();
            using var audioFile = new AudioFileReader(audioFilePath);
            var waveOut = new WaveOut();
            _currentWaveOut = waveOut;
            try
            {
                waveOut.Init(audioFile);
                waveOut.Play();

                while (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        waveOut.Stop();
                        break;
                    }
                    await Task.Delay(50, cancellationToken);
                }
            }
            finally
            {
                try
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                }
                catch { }
                if (ReferenceEquals(_currentWaveOut, waveOut))
                    _currentWaveOut = null;
            }
        }
        catch { }
    }

    public void StopSpeaking()
    {
        _activeCts?.Cancel();
        _activeCts = null;
        try
        {
            _currentWaveOut?.Stop();
            _currentWaveOut?.Dispose();
            _currentWaveOut = null;
        }
        catch { }
    }

    public void Dispose()
    {
        StopSpeaking();
    }

    public static string BuildSsml(string text, string voice, int rate)
    {
        var ratePercent = rate >= 0 ? $"+{rate * 10}%" : $"{rate * 10}%";
        var escapedText = EscapeXml(text);
        return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='ru-RU'><voice name='{voice}'><prosody rate='{ratePercent}' pitch='+0Hz'>{escapedText}</prosody></voice></speak>";
    }

    private static string EscapeXml(string input)
    {
        return SecurityElement.Escape(input) ?? input;
    }
}
