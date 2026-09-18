using System.Net.Http;

namespace ReLaitis.Audio.Synthesis;

/// <summary>
/// Синтез речи VoiceRSS без ключей (канонический аналог 0h.cs из Laitis).
/// Возвращает MP3 поток с кэшированием в VoiceAudioCache.
/// </summary>
public class VoiceRssTtsService : ISpeechSynthesizer
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Name => "VoiceRSS TTS";

    static VoiceRssTtsService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        HttpClient.DefaultRequestHeaders.Referrer = new Uri("http://www.voicerss.org/");
    }

    public async Task<byte[]> SynthesizeAsync(string text, string language = "ru-RU", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var voiceKey = $"VoiceRSS_{language}";
        var cached = VoiceAudioCache.GetCachedAudio(text, voiceKey, isWav: false);
        if (cached != null)
            return cached;

        try
        {
            var rnd = Random.Shared.NextDouble().ToString("0.0000000000000000", System.Globalization.CultureInfo.InvariantCulture);
            var lang = language.ToLowerInvariant(); // "ru-ru"
            var url = $"http://www.voicerss.org/controls/speech.ashx?hl={Uri.EscapeDataString(lang)}&src={Uri.EscapeDataString(text)}&c=mp3&rnd={rnd}";

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
