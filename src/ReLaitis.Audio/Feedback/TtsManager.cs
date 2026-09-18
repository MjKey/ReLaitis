using System.Text;
using System.Text.Json;
using NAudio.Wave;
using ReLaitis.Audio.Synthesis;
using ReLaitis.Core.Interfaces;
using ReLaitis.Core.Models;

namespace ReLaitis.Audio.Feedback;

/// <summary>
/// Единый менеджер и маршрутизатор синтеза речи ReLaitis.
/// Поддерживает:
/// 1. Microsoft Edge Neural TTS (Светлана, Дмитрий - бесплатно, нейросеть).
/// 2. Google Chromium TTS (Translate TTS + Cloud TTS v2).
/// 3. Bing Translator TTS.
/// 4. VoiceRSS TTS.
/// 5. Windows SAPI (100% офлайн системные голоса).
/// 6. OpenAI TTS (при указании API ключа).
/// При ошибке сети автоматически переключается на локальный офлайн SAPI.
/// </summary>
public class TtsManager : IVoiceFeedback, IDisposable
{
    private readonly WindowsVoiceFeedback _sapiService;
    private readonly EdgeNeuralTtsService _edgeService;
    private readonly GoogleChromiumTtsService _googleChromiumService;
    private readonly BingTranslatorTtsService _bingTranslatorService;
    private readonly VoiceRssTtsService _voiceRssService;
    private readonly HttpClient _httpClient;

    private UserSettings _settings;
    private WaveOut? _currentWaveOut;

    public TtsManager(UserSettings? settings = null)
    {
        _settings = settings ?? new UserSettings();
        _sapiService = new WindowsVoiceFeedback();
        _edgeService = new EdgeNeuralTtsService();
        _googleChromiumService = new GoogleChromiumTtsService();
        _bingTranslatorService = new BingTranslatorTtsService();
        _voiceRssService = new VoiceRssTtsService();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        ApplySettings(_settings);
    }

    public void ApplySettings(UserSettings settings)
    {
        _settings = settings;

        // Настройка SAPI
        _sapiService.SetVoice(settings.SapiVoiceName);
        _sapiService.SetRate(settings.SpeechRate);

        // Настройка Edge Neural
        _edgeService.CurrentVoice = string.IsNullOrWhiteSpace(settings.EdgeVoiceName)
            ? "ru-RU-SvetlanaNeural"
            : settings.EdgeVoiceName;
        _edgeService.SpeechRate = settings.SpeechRate;
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        StopSpeaking();

        switch (_settings.TtsProvider)
        {
            case TtsProviderType.MicrosoftEdgeNeural:
                try
                {
                    await _edgeService.SpeakAsync(text, cancellationToken);
                    return;
                }
                catch
                {
                    // Резервный офлайн-фоллбэк на Windows SAPI
                    await _sapiService.SpeakAsync(text, cancellationToken);
                    return;
                }

            case TtsProviderType.GoogleChromium:
                try
                {
                    var audio = await _googleChromiumService.SynthesizeAsync(text, ct: cancellationToken);
                    if (audio != null && audio.Length > 0)
                    {
                        await PlayAudioBytesAsync(audio, cancellationToken);
                        return;
                    }
                }
                catch { }
                await _sapiService.SpeakAsync(text, cancellationToken);
                return;

            case TtsProviderType.BingTranslator:
                try
                {
                    var audio = await _bingTranslatorService.SynthesizeAsync(text, ct: cancellationToken);
                    if (audio != null && audio.Length > 0)
                    {
                        await PlayAudioBytesAsync(audio, cancellationToken);
                        return;
                    }
                }
                catch { }
                await _sapiService.SpeakAsync(text, cancellationToken);
                return;

            case TtsProviderType.VoiceRss:
                try
                {
                    var audio = await _voiceRssService.SynthesizeAsync(text, ct: cancellationToken);
                    if (audio != null && audio.Length > 0)
                    {
                        await PlayAudioBytesAsync(audio, cancellationToken);
                        return;
                    }
                }
                catch { }
                await _sapiService.SpeakAsync(text, cancellationToken);
                return;

            case TtsProviderType.OpenAiTts when !string.IsNullOrWhiteSpace(_settings.OpenAiApiKey):
                try
                {
                    await SpeakOpenAiAsync(text, _settings.OpenAiApiKey, _settings.OpenAiVoice, cancellationToken);
                    return;
                }
                catch
                {
                    await _sapiService.SpeakAsync(text, cancellationToken);
                    return;
                }

            case TtsProviderType.WindowsSapi:
            default:
                await _sapiService.SpeakAsync(text, cancellationToken);
                break;
        }
    }

    private async Task PlayAudioBytesAsync(byte[] audioBytes, CancellationToken cancellationToken)
    {
        if (audioBytes == null || audioBytes.Length == 0) return;

        var tcs = new TaskCompletionSource();
        using var memStream = new MemoryStream(audioBytes);
        WaveStream waveStream;
        if (audioBytes.Length > 4 && audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F')
        {
            waveStream = new WaveFileReader(memStream);
        }
        else
        {
            waveStream = new Mp3FileReader(memStream);
        }

        using (waveStream)
        {
            var waveOut = new WaveOut();
            _currentWaveOut = waveOut;
            try
            {
                waveOut.Init(waveStream);
                waveOut.PlaybackStopped += (s, e) => tcs.TrySetResult();
                waveOut.Play();

                using (cancellationToken.Register(() =>
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
    }

    private async Task SpeakOpenAiAsync(string text, string apiKey, string voice, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            model = "tts-1",
            input = text,
            voice = string.IsNullOrWhiteSpace(voice) ? "alloy" : voice
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var mp3Bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var tcs = new TaskCompletionSource();
        using var memStream = new MemoryStream(mp3Bytes);
        using var mp3Reader = new Mp3FileReader(memStream);

        var waveOut = new WaveOut();
        _currentWaveOut = waveOut;
        try
        {
            waveOut.Init(mp3Reader);
            waveOut.PlaybackStopped += (s, e) => tcs.TrySetResult();
            waveOut.Play();

            using (cancellationToken.Register(() =>
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
        await _sapiService.PlayAudioAsync(audioFilePath, cancellationToken);
    }

    public void StopSpeaking()
    {
        _edgeService.StopSpeaking();
        _sapiService.StopSpeaking();
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
        _sapiService.Dispose();
        _edgeService.Dispose();
        _httpClient.Dispose();
    }
}
