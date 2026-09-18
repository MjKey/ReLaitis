namespace ReLaitis.Core.Interfaces;

/// <summary>
/// Интерфейс голосовой и аудио обратной связи (TTS и звуковые эффекты).
/// </summary>
public interface IVoiceFeedback
{
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
    Task PlayAudioAsync(string audioFilePath, CancellationToken cancellationToken = default);
    void StopSpeaking();
}
