using System.Net.Http;

namespace ReLaitis.Audio.Synthesis;

/// <summary>
/// Синтез речи Bing Translator без регистрации и ключей (канонический аналог Zh.cs из Laitis).
/// Возвращает MP3 поток с кэшированием в VoiceAudioCache.
/// </summary>
public class BingTranslatorTtsService : ISpeechSynthesizer
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Name => "Bing Translator TTS";

    static BingTranslatorTtsService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<byte[]> SynthesizeAsync(string text, string language = "ru-RU", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var voiceKey = $"Bing_{language}";
        var cached = VoiceAudioCache.GetCachedAudio(text, voiceKey, isWav: false);
        if (cached != null)
            return cached;

        try
        {
            var langCode = language.Split('-')[0]; // "ru"
            var url = $"https://www.bing.com/tspeak?&format=audio%2Fmp3&IG=3EDFE919162E4D3AACA4D7A04A66ED29&IID=translator.5035.1&options=female&text={Uri.EscapeDataString(text)}&language={Uri.EscapeDataString(langCode)}";

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

        return [];
    }
}
