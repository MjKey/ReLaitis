using System.IO;
using System.Speech.Synthesis;

namespace ReLaitis.Audio.Synthesis;

/// <summary>
/// Синтез речи Windows SAPI (канонический аналог 1h.cs из Laitis).
/// 100% офлайн, использует встроенные системные голоса Windows с кэшированием в VoiceAudioCache.
/// </summary>
public class WindowsSapiTtsService : ISpeechSynthesizer
{
    public string Name => "Windows SAPI TTS";
    public string? SelectedVoice { get; set; }

    public Task<byte[]> SynthesizeAsync(string text, string language = "ru-RU", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult<byte[]>([]);

        var voiceKey = $"SAPI_{SelectedVoice ?? "Default"}";
        var cached = VoiceAudioCache.GetCachedAudio(text, voiceKey, isWav: true);
        if (cached != null)
            return Task.FromResult(cached);

        try
        {
            using var synth = new SpeechSynthesizer();
            if (!string.IsNullOrWhiteSpace(SelectedVoice))
            {
                try
                {
                    synth.SelectVoice(SelectedVoice);
                }
                catch { }
            }

            using var ms = new MemoryStream();
            synth.SetOutputToWaveStream(ms);
            synth.Speak(text);

            var wavBytes = ms.ToArray();
            if (wavBytes.Length > 100)
            {
                VoiceAudioCache.SaveAudio(text, voiceKey, wavBytes, isWav: true);
                return Task.FromResult(wavBytes);
            }
        }
        catch { }

        return Task.FromResult<byte[]>([]);
    }
}
