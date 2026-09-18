namespace ReLaitis.Audio.Synthesis;

/// <summary>
/// Интерфейс генератора аудиоданных синтеза речи.
/// Возвращает закодированный аудиопоток (MP3 или WAV).
/// </summary>
public interface ISpeechSynthesizer
{
    string Name { get; }
    Task<byte[]> SynthesizeAsync(string text, string language = "ru-RU", CancellationToken ct = default);
}
