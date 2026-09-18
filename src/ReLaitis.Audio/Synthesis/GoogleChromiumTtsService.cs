using System.IO;
using System.Net.Http;

namespace ReLaitis.Audio.Synthesis;

/// <summary>
/// Синтез речи Google без ключей (канонический аналог mg.cs из Laitis).
/// Использует эндпоинты Chromium v2 и Google Translate TTS с кэшированием VoiceAudioCache.
/// </summary>
public class GoogleChromiumTtsService : ISpeechSynthesizer
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Name => "Google Chromium TTS";

    static GoogleChromiumTtsService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<byte[]> SynthesizeAsync(string text, string language = "ru-RU", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var voiceKey = $"Google_{language}";
        var cached = VoiceAudioCache.GetCachedAudio(text, voiceKey, isWav: false);
        if (cached != null)
            return cached;

        var langCode = language.Split('-')[0]; // "ru" from "ru-RU"

        // 1. Попытка через Google Translate TTS
        try
        {
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl={Uri.EscapeDataString(langCode)}&q={Uri.EscapeDataString(text)}";
            using var resp = await HttpClient.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length > 100)
                {
                    VoiceAudioCache.SaveAudio(text, voiceKey, bytes, isWav: false);
                    return bytes;
                }
            }
        }
        catch { }

        // 2. Резервная попытка через Chromium v2 API
        try
        {
            var dummyKey = Guid.NewGuid().ToString("N");
            var url2 = $"https://www.google.com/speech-api/v2/synthesize?enc=mpeg&client=chromium&key={dummyKey}&text={Uri.EscapeDataString(text)}&lang={Uri.EscapeDataString(language)}";
            using var resp2 = await HttpClient.GetAsync(url2, ct);
            if (resp2.IsSuccessStatusCode)
            {
                var bytes = await resp2.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length > 100)
                {
                    VoiceAudioCache.SaveAudio(text, voiceKey, bytes, isWav: false);
                    return bytes;
                }
            }
        }
        catch { }

        return [];
    }
}
