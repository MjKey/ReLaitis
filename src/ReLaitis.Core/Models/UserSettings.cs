namespace ReLaitis.Core.Models;

/// <summary>
/// Режим прослушивания команд.
/// </summary>
public enum ListeningMode
{
    Continuous = 0,
    PushToTalk = 1,
    WakeWord = 2
}

/// <summary>
/// Движок распознавания речи (Vosk или OpenAI Whisper).
/// </summary>
public enum SpeechEngineType
{
    Vosk = 0,
    Whisper = 1,
    GoogleFullDuplex = 2,
    WindowsSapi = 3
}

/// <summary>
/// Провайдер синтеза речи (TTS / Озвучка ответов).
/// </summary>
public enum TtsProviderType
{
    WindowsSapi = 0,
    MicrosoftEdgeNeural = 1,
    OpenAiTts = 2,
    GoogleChromium = 3,
    BingTranslator = 4,
    VoiceRss = 5
}

/// <summary>
/// Пользовательские настройки ReLaitis, сохраняемые в UserSettings.json.
/// </summary>
public class UserSettings
{
    // Распознавание речи
    public SpeechEngineType SpeechEngine { get; set; } = SpeechEngineType.Vosk;
    public string WhisperModel { get; set; } = "base";
    public int SelectedAudioDevice { get; set; } = 0;
    public ListeningMode ListeningMode { get; set; } = ListeningMode.Continuous;
    public string PushToTalkKey { get; set; } = "CapsLock";
    public string WakeWord { get; set; } = "лэйтис, джарвис, слушай";
    public int WakeWordTimeoutSeconds { get; set; } = 6;

    // Синтез речи (TTS)
    public TtsProviderType TtsProvider { get; set; } = TtsProviderType.MicrosoftEdgeNeural;
    public string SapiVoiceName { get; set; } = string.Empty;
    public string EdgeVoiceName { get; set; } = "ru-RU-SvetlanaNeural";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiVoice { get; set; } = "alloy";
    public int SpeechRate { get; set; } = 0; // -10 .. +10

    // Звуки и автозапуск
    public bool SoundFeedbackEnabled { get; set; } = true;
    public bool AutoStartWithWindows { get; set; } = false;

    // Оверлей HUD
    public bool ShowHud { get; set; } = false; // По умолчанию выключен
    public double HudOpacity { get; set; } = 0.95;
    public bool StartMinimizedToTray { get; set; } = false;
}
