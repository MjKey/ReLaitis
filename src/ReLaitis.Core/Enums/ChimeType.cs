namespace ReLaitis.Core.Enums;

/// <summary>
/// Тип звукового сигнала обратной связи.
/// </summary>
public enum ChimeType
{
    /// <summary>
    /// Звук готовности слушать (активация Wake Word или PTT).
    /// </summary>
    ListeningStart,

    /// <summary>
    /// Звук успешного распознавания и выполнения команды.
    /// </summary>
    Success,

    /// <summary>
    /// Звук неизвестной команды или ошибки.
    /// </summary>
    Error
}
