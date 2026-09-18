using System.Text.Json.Serialization;
using ReLaitis.Core.Helpers;

namespace ReLaitis.Core.Models;

/// <summary>
/// Голосовая команда, содержащая фразы активации и цепочку действий.
/// </summary>
public class VoiceCommand
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Фразы-триггеры (синонимы, вариации, шаблоны с {переменными}).
    /// </summary>
    [JsonPropertyName("phrases")]
    [JsonConverter(typeof(StringListOrSingleJsonConverter))]
    public List<string> Phrases { get; set; } = [];

    /// <summary>
    /// Последовательность действий, выполняемых командой.
    /// </summary>
    [JsonPropertyName("actions")]
    public List<CommandAction> Actions { get; set; } = [];

    // Удобство для LLM: поддержка ключа "triggers"
    [JsonPropertyName("triggers")]
    public List<string>? TriggersFallback
    {
        get => null;
        set { if (value != null && Phrases.Count == 0) Phrases = value; }
    }



    /// <summary>
    /// Главная фраза для отображения в списке.
    /// </summary>
    [JsonIgnore]
    public string PrimaryPhrase => Phrases.Count > 0 ? Phrases[0] : "(нет фраз)";

    /// <summary>
    /// Заголовок команды для отображения в списке (все фразы-триггеры через запятую или кастомное имя).
    /// </summary>
    [JsonIgnore]
    public string DisplayTitle
    {
        get
        {
            if (Phrases.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(Name) || Phrases.Contains(Name, StringComparer.OrdinalIgnoreCase))
                {
                    return string.Join(", ", Phrases);
                }
                return Name;
            }
            return !string.IsNullOrWhiteSpace(Name) ? Name : "(без названия)";
        }
    }

    [JsonIgnore]
    public string DisplaySubtitle
    {
        get
        {
            // Если у команды задано кастомное имя, отличное от фраз, выводим фразы
            if (!string.IsNullOrWhiteSpace(Name) && Phrases.Count > 0 && !Phrases.Contains(Name, StringComparer.OrdinalIgnoreCase))
            {
                var phrasesStr = string.Join(", ", Phrases);
                if (Actions.Count > 0)
                    return $"{phrasesStr} • {Actions[0].DisplayDescription}";
                return phrasesStr;
            }

            if (Actions.Count == 0)
                return "Нет действий";

            if (Actions.Count == 1)
                return Actions[0].DisplayDescription;

            // Превью цепочки действий (как в Laitis)
            var preview = Actions[0].DisplayDescription;
            if (Actions.Count > 1)
            {
                preview += $" • {Actions[1].DisplayDescription}";
                if (Actions.Count > 2)
                    preview += $" (+{Actions.Count - 2})";
            }
            return preview;
        }
    }

    [JsonIgnore]
    public string PhrasesBadge => Phrases.Count.ToString();

    [JsonIgnore]
    public string ActionsBadge => Actions.Count.ToString();

    public override string ToString() => $"{Name} [{string.Join(" | ", Phrases)}] ({Actions.Count} actions)";
}
