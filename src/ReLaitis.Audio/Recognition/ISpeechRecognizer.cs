namespace ReLaitis.Audio.Recognition;

/// <summary>
/// Общий интерфейс движка распознавания речи (Vosk, Whisper и др.).
/// </summary>
public interface ISpeechRecognizer : IDisposable
{
    /// <summary>
    /// Событие промежуточного результата распознавания (во время речи).
    /// </summary>
    event EventHandler<string>? PartialResultRecognized;

    /// <summary>
    /// Событие окончательного результата фразы.
    /// </summary>
    event EventHandler<string>? FinalResultRecognized;

    /// <summary>
    /// Загружена ли модель распознавания речи.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Загрузка модели из указанного пути.
    /// </summary>
    bool LoadModel(string modelPath);

    /// <summary>
    /// Обработка порции аудиоданных (16kHz 16-bit Mono PCM).
    /// </summary>
    void ProcessAudio(byte[] data);

    /// <summary>
    /// Сброс буфера распознавания и форсированное завершение текущей фразы.
    /// </summary>
    void Reset();
}
