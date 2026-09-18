using System.Text.Json.Serialization;
using ReLaitis.Core.Helpers;

namespace ReLaitis.Core.Models;

/// <summary>
/// Пакет голосовых команд (профиль).
/// Может быть глобальным или привязанным к процессам/окнам.
/// </summary>
public class CommandPack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = "ru-RU";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Фильтр процессов (например, "chrome.exe/firefox.exe" или пустая строка для глобального пакета).
    /// </summary>
    [JsonPropertyName("processFilter")]
    public string ProcessFilter { get; set; } = string.Empty;

    /// <summary>
    /// Группы команд внутри пакета.
    /// </summary>
    [JsonPropertyName("groups")]
    public List<CommandGroup> Groups { get; set; } = [];

    /// <summary>
    /// Удобство для LLM и ручного редактирования:
    /// если автор пакета не создал groups, а сразу перечислил commands на уровне пака,
    /// автоматически объединяем их в группу "Общие".
    /// </summary>
    [JsonPropertyName("commands")]
    public List<VoiceCommand>? DirectCommands
    {
        get => null;
        set
        {
            if (value != null && value.Count > 0)
            {
                var group = Groups.FirstOrDefault(g => g.Name == "Общие" || g.Name == "Команды");
                if (group == null)
                {
                    group = new CommandGroup { Name = "Общие", Commands = [] };
                    Groups.Add(group);
                }
                group.Commands.AddRange(value);
            }
        }
    }

    /// <summary>
    /// Правила автозамены слов пакета.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<Rule> Rules { get; set; } = [];

    /// <summary>
    /// Предопределенные переменные пакета.
    /// </summary>
    [JsonPropertyName("variables")]
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);



    [JsonIgnore]
    public int TotalCommandsCount => Groups.Sum(g => g.Commands.Count);

    [JsonIgnore]
    public string DisplaySubtitle
    {
        get
        {
            var cmdText = RussianPluralizer.Format(TotalCommandsCount, "команда", "команды", "команд");
            return !string.IsNullOrEmpty(ProcessFilter)
                ? $"Фильтр: {ProcessFilter} • {cmdText}"
                : $"Глобальный пакет • {cmdText}";
        }
    }

    public override string ToString() => $"{Name} ({Culture}, {Groups.Count} groups, Active={IsActive})";
}
