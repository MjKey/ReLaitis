using System.Text.Json.Serialization;

namespace ReLaitis.Core.Models;

/// <summary>
/// Группа (категория) голосовых команд внутри пакета.
/// </summary>
public class CommandGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("commands")]
    public List<VoiceCommand> Commands { get; set; } = [];



    public override string ToString() => $"{Name} ({Commands.Count} commands)";
}
