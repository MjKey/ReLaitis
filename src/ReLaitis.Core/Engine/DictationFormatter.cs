using System.Text;

namespace ReLaitis.Core.Engine;

/// <summary>
/// Форматировщик потоковой диктовки: автоматическая замена голосовых знаков препинания
/// («запятая», «точка», «восклицательный знак», «новая строка») и капитализация предложений.
/// </summary>
public static class DictationFormatter
{
    public static string FormatSpokenText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i].ToLowerInvariant().Replace('ё', 'е');

            var punctuation = word switch
            {
                "запятая" => ",",
                "точка" => ".",
                "восклицательный" when i + 1 < words.Length && words[i + 1].Equals("знак", StringComparison.OrdinalIgnoreCase) => "!",
                "вопросительный" when i + 1 < words.Length && words[i + 1].Equals("знак", StringComparison.OrdinalIgnoreCase) => "?",
                "двоеточие" => ":",
                "тире" or "дефис" => " -",
                "новая" when i + 1 < words.Length && words[i + 1].Equals("строка", StringComparison.OrdinalIgnoreCase) => "\n",
                "абзац" => "\n",
                _ => null
            };

            if (punctuation != null)
            {
                if (punctuation is "!" or "?" || (punctuation == "\n" && words[i].Equals("новая", StringComparison.OrdinalIgnoreCase)))
                {
                    i++; // Пропускаем слово «знак» или «строка»
                }

                if (punctuation is "," or "." or "!" or "?" or ":" or "\n" && sb.Length > 0 && sb[^1] == ' ')
                {
                    sb.Length--;
                }

                sb.Append(punctuation);
                if (punctuation != "\n")
                    sb.Append(' ');
            }
            else
            {
                var wordToAdd = words[i];
                var currentText = sb.ToString();

                if (sb.Length == 0 || currentText.EndsWith(". ") || currentText.EndsWith("! ") || currentText.EndsWith("? ") || currentText.EndsWith("\n"))
                {
                    wordToAdd = char.ToUpper(wordToAdd[0]) + (wordToAdd.Length > 1 ? wordToAdd[1..] : "");
                }

                sb.Append(wordToAdd).Append(' ');
            }
        }

        return sb.ToString().TrimEnd();
    }
}
