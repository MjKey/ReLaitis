using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ReLaitis.Core.Models;
using MediaColor = System.Windows.Media.Color;

namespace ReLaitis.UI.Models;

public enum HistoryEntryType
{
    Executed,   // Команда успешно сопоставлена и выполнена
    Unmatched,  // Фраза услышана, но совпадений среди команд не найдено
    Dictation,  // Ввод текста через режим диктовки
    Meta,       // Мета-команда глушения или управления
    Error       // Ошибка выполнения
}

/// <summary>
/// Расширенная модель записи журнала распознавания и выполнения команд.
/// </summary>
public class HistoryItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public DateTime FullDateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Точная распознанная фраза, услышанная микрофоном.
    /// </summary>
    public string Phrase { get; set; } = string.Empty;

    public string CommandName { get; set; } = "-";
    public string PackName { get; set; } = "-";
    public string ConfidenceText { get; set; } = "100%";
    public double Confidence { get; set; } = 1.0;
    public string Process { get; set; } = "-";
    public string WindowTitle { get; set; } = "-";
    public long DurationMs { get; set; } = 0;
    public HistoryEntryType Type { get; set; } = HistoryEntryType.Executed;
    public Dictionary<string, string> ExtractedVariables { get; set; } = [];
    public VoiceCommand? CommandRef { get; set; }
    public IReadOnlyList<CommandAction>? Actions { get; set; }

    public bool IsExecuted => Type == HistoryEntryType.Executed;
    public bool IsUnmatched => Type == HistoryEntryType.Unmatched;
    public bool IsDictation => Type == HistoryEntryType.Dictation;
    public bool IsMeta => Type == HistoryEntryType.Meta;

    public string StatusText => Type switch
    {
        HistoryEntryType.Executed => "ВЫПОЛНЕНО",
        HistoryEntryType.Unmatched => "НЕ СОВПАЛО",
        HistoryEntryType.Dictation => "ДИКТОВКА",
        HistoryEntryType.Meta => "МЕТА",
        HistoryEntryType.Error => "ОШИБКА",
        _ => "ИНФО"
    };

    public SolidColorBrush StatusBg => Type switch
    {
        HistoryEntryType.Executed => new SolidColorBrush(MediaColor.FromArgb(40, 16, 185, 129)),
        HistoryEntryType.Unmatched => new SolidColorBrush(MediaColor.FromArgb(40, 245, 158, 11)),
        HistoryEntryType.Dictation => new SolidColorBrush(MediaColor.FromArgb(40, 147, 51, 234)),
        HistoryEntryType.Meta => new SolidColorBrush(MediaColor.FromArgb(40, 59, 130, 246)),
        HistoryEntryType.Error => new SolidColorBrush(MediaColor.FromArgb(40, 239, 68, 68)),
        _ => new SolidColorBrush(MediaColor.FromArgb(40, 100, 116, 139))
    };

    public SolidColorBrush StatusFg => Type switch
    {
        HistoryEntryType.Executed => new SolidColorBrush(MediaColor.FromRgb(52, 211, 153)),
        HistoryEntryType.Unmatched => new SolidColorBrush(MediaColor.FromRgb(251, 191, 36)),
        HistoryEntryType.Dictation => new SolidColorBrush(MediaColor.FromRgb(216, 180, 254)),
        HistoryEntryType.Meta => new SolidColorBrush(MediaColor.FromRgb(147, 197, 253)),
        HistoryEntryType.Error => new SolidColorBrush(MediaColor.FromRgb(252, 165, 165)),
        _ => new SolidColorBrush(MediaColor.FromRgb(203, 213, 225))
    };

    public SolidColorBrush StatusBorder => Type switch
    {
        HistoryEntryType.Executed => new SolidColorBrush(MediaColor.FromRgb(16, 185, 129)),
        HistoryEntryType.Unmatched => new SolidColorBrush(MediaColor.FromRgb(245, 158, 11)),
        HistoryEntryType.Dictation => new SolidColorBrush(MediaColor.FromRgb(147, 51, 234)),
        HistoryEntryType.Meta => new SolidColorBrush(MediaColor.FromRgb(59, 130, 246)),
        HistoryEntryType.Error => new SolidColorBrush(MediaColor.FromRgb(239, 68, 68)),
        _ => new SolidColorBrush(MediaColor.FromRgb(100, 116, 139))
    };

    public SolidColorBrush ConfidenceBrush
    {
        get
        {
            if (Confidence >= 0.85)
                return new SolidColorBrush(MediaColor.FromRgb(52, 211, 153));
            if (Confidence >= 0.60)
                return new SolidColorBrush(MediaColor.FromRgb(251, 191, 36));
            return new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
        }
    }

    public string FormattedDuration => DurationMs > 0 ? $"{DurationMs} мс" : "-";

    public string ProcessAndWindow
    {
        get
        {
            if (!string.IsNullOrEmpty(WindowTitle) && WindowTitle != "-" && WindowTitle != Process)
                return $"{Process} • {WindowTitle}";
            return Process;
        }
    }

    public string FormattedVariables => ExtractedVariables.Count > 0
        ? string.Join(", ", ExtractedVariables.Select(kv => $"{{{kv.Key}}} = \"{kv.Value}\""))
        : "-";

    public bool HasVariables => ExtractedVariables.Count > 0;
    public bool HasActions => Actions != null && Actions.Count > 0;
    public int ActionsCount => Actions?.Count ?? 0;

    public string ActionsSummary => Actions != null && Actions.Count > 0
        ? $"{Actions.Count} действ. ({string.Join(" → ", Actions.Take(3).Select(a => a.Type.ToString()))}{(Actions.Count > 3 ? "..." : "")})"
        : "-";

    public HistoryItem() { }

    public HistoryItem(string time, string phrase, string commandName, string confidenceText, string process)
    {
        Time = time;
        Phrase = phrase;
        CommandName = commandName;
        ConfidenceText = confidenceText;
        Process = process;
    }
}

public record HistoryActionDisplay(string OrderText, string ActionTitle, string ActionDetail);

