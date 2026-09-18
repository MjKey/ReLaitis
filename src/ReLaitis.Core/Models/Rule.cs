using System.Text.Json.Serialization;

namespace ReLaitis.Core.Models;

/// <summary>
/// Правило автозамены слов (канонический формат Laitis, секция "Y").
/// Применяется к распознанному голосовому тексту для нормализации фраз ("W" -> "R").
/// </summary>
public class Rule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("replacement")]
    public string Replacement { get; set; } = string.Empty;



    public Rule() { }

    public Rule(string word, string replacement)
    {
        Word = word;
        Replacement = replacement;
    }

    public override string ToString() => $"Rule: '{Word}' -> '{Replacement}'";
}
