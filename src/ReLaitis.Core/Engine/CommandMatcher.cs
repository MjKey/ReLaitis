using System.Text.RegularExpressions;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Engine;

/// <summary>
/// Результат успешного сопоставления фразы с командой.
/// </summary>
public record CommandMatchResult(
    VoiceCommand Command,
    CommandPack Pack,
    Dictionary<string, string> ExtractedVariables,
    double Confidence,
    string SpokenPhrase = "",
    long ExecutionDurationMs = 0);

/// <summary>
/// Матчер голосовых команд: сопоставление фраз, извлечение слотов/переменных и проверка контекста процессов.
/// </summary>
public class CommandMatcher
{
    /// <summary>
    /// Ищет подходящую команду среди всех доступных пакетов с учетом текущего активного процесса и предусловий команд.
    /// </summary>
    public CommandMatchResult? FindMatch(
        string spokenText,
        IEnumerable<CommandPack> packs,
        string activeProcessName,
        Func<string, bool>? isProcessActive = null,
        Func<string, bool>? isProcessRunning = null)
    {
        if (string.IsNullOrWhiteSpace(spokenText))
            return null;

        var normalizedSpoken = NormalizePhrase(spokenText);
        var activeChecker = isProcessActive ?? (filter => IsProcessMatchingFilter(filter, activeProcessName));

        CommandMatchResult? bestMatch = null;

        foreach (var pack in packs)
        {
            if (!pack.IsActive)
                continue;

            // Проверяем фильтр процесса пакета
            if (!activeChecker(pack.ProcessFilter))
                continue;

            foreach (var group in pack.Groups)
            {
                if (!group.IsEnabled)
                    continue;

                foreach (var command in group.Commands)
                {
                    if (!command.IsEnabled)
                        continue;

                    // Проверяем предусловия самой команды (первые действия IfProcessSelected / IfProcessExists)
                    if (!CheckCommandPreconditions(command, activeChecker, isProcessRunning))
                        continue;

                    foreach (var phrasePattern in command.Phrases)
                    {
                        var match = MatchPhrase(normalizedSpoken, phrasePattern, command, pack);
                        if (match != null)
                        {
                            // Если точное совпадение (1.0) - возвращаем сразу
                            if (match.Confidence >= 0.99)
                                return match;

                            if (bestMatch == null || match.Confidence > bestMatch.Confidence)
                                bestMatch = match;
                        }
                    }
                }
            }
        }

        return bestMatch != null ? bestMatch with { SpokenPhrase = spokenText } : null;
    }

    /// <summary>
    /// Проверяет начальные условия команды (IfProcessSelected, IfProcessExists до первого реального действия).
    /// Если условия не выполнены, команда не должна перехватывать голос.
    /// </summary>
    private static bool CheckCommandPreconditions(
        VoiceCommand command,
        Func<string, bool> isProcessActive,
        Func<string, bool>? isProcessRunning)
    {
        foreach (var action in command.Actions)
        {
            if (action.Type == ActionType.IfProcessSelected)
            {
                if (action.Parameters.Length > 0 && !string.IsNullOrWhiteSpace(action.Parameters[0]))
                {
                    if (!isProcessActive(action.Parameters[0]))
                        return false;
                }
            }
            else if (action.Type == ActionType.IfProcessExists)
            {
                if (action.Parameters.Length > 0 && !string.IsNullOrWhiteSpace(action.Parameters[0]))
                {
                    if (isProcessRunning != null && !isProcessRunning(action.Parameters[0]))
                        return false;
                }
            }
            else if (IsExecutableAction(action.Type))
            {
                // Достигли первого реального действия макроса - начальные предусловия успешно пройдены
                break;
            }
        }

        return true;
    }

    private static bool IsExecutableAction(ActionType type) =>
        type is not (ActionType.IfProcessSelected or ActionType.IfProcessExists or ActionType.Comment);

    /// <summary>
    /// Сопоставляет произнесенную фразу с шаблоном фразы команды (поддерживает слоты {переменная}).
    /// </summary>
    public CommandMatchResult? MatchPhrase(
        string normalizedSpoken,
        string phrasePattern,
        VoiceCommand command,
        CommandPack pack)
    {
        if (string.IsNullOrWhiteSpace(phrasePattern))
            return null;

        var normalizedPattern = NormalizePhrase(phrasePattern);

        // 1. Прямое точное совпадение
        if (string.Equals(normalizedSpoken, normalizedPattern, StringComparison.OrdinalIgnoreCase))
        {
            return new CommandMatchResult(command, pack, new Dictionary<string, string>(), 1.0);
        }

        // 2. Если в шаблоне есть слоты/переменные: "открой {app}", "сколько будет {x} + {y}", "{выражение}", "{название}"
        if (normalizedPattern.Contains('{') && normalizedPattern.Contains('}'))
        {
            var (regexPattern, slotNames) = ConvertPatternToRegex(normalizedPattern);
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            var m = regex.Match(normalizedSpoken);

            if (m.Success)
            {
                var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var slotName in slotNames)
                {
                    var group = m.Groups[slotName];
                    if (group.Success)
                    {
                        variables[slotName] = group.Value.Trim();
                    }
                }

                // Рассчитываем специфичность шаблона: голый {wildcard} не должен перебивать команды со словами
                var literalCharsCount = normalizedPattern.Length - slotNames.Sum(s => s.Length + 2);
                double confidence;

                if (literalCharsCount <= 0)
                {
                    // Чистый вайлдкард ({выражение} или {название}) без единого фиксированного слова:
                    // Имеет минимальный базовый приоритет (0.35), чтобы любая команда со словами побеждала его.
                    confidence = 0.35;

                    // Если слот называется {выражение} (калькулятор): проверяем, содержит ли фраза числа или мат. термины
                    if (slotNames.Any(s => s.Equals("выражение", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (ContainsMathTokens(normalizedSpoken))
                        {
                            confidence = 0.65;
                        }
                        else
                        {
                            // Обычная речь без чисел/операций не должна сопоставляться с выражением калькулятора
                            return null;
                        }
                    }
                }
                else
                {
                    var specificityBonus = Math.Clamp((double)literalCharsCount / 10.0, 0.0, 1.0) * 0.18;
                    confidence = 0.80 + specificityBonus;
                }

                return new CommandMatchResult(command, pack, variables, confidence);
            }
        }

        // 3. Нечеткое сравнение (Fuzzy Match по Левенштейну для коротких опечаток STT)
        if (normalizedSpoken.Length >= 4 && normalizedPattern.Length >= 4)
        {
            var distance = LevenshteinDistance(normalizedSpoken, normalizedPattern);
            var maxLen = Math.Max(normalizedSpoken.Length, normalizedPattern.Length);
            var similarity = 1.0 - (double)distance / maxLen;

            if (similarity >= 0.82)
            {
                return new CommandMatchResult(command, pack, new Dictionary<string, string>(), similarity);
            }
        }

        return null;
    }

    public static string NormalizePhrase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lower = text.Trim().ToLowerInvariant();
        // Заменяем букву ё на е для стабильного распознавания
        lower = lower.Replace('ё', 'е');

        // Оставляем буквы, цифры, пробелы, скобки слотов и математические операторы
        var chars = lower.Select(c =>
            char.IsLetterOrDigit(c) || c is ' ' or '{' or '}' or '%' or '+' or '-' or '*' or '/' or '='
                ? c
                : ' ').ToArray();
        var cleaned = new string(chars);

        // Схлопываем множественные пробелы
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static (string RegexPattern, List<string> SlotNames) ConvertPatternToRegex(string pattern)
    {
        var slotNames = new List<string>();
        var placeholders = new List<(string Placeholder, string SlotName)>();

        // 1. Заменяем {имя_слота} на безопасный плейсхолдер
        var withPlaceholders = Regex.Replace(pattern, @"\{([a-zA-Z0-9_\u0400-\u04FF]+)\}", match =>
        {
            var slotName = match.Groups[1].Value;
            var placeholder = $"__SLOT_{slotNames.Count}__";
            slotNames.Add(slotName);
            placeholders.Add((placeholder, slotName));
            return placeholder;
        });

        // 2. Экранируем спецсимволы регулярки (например, "+", "-", "*")
        var escaped = Regex.Escape(withPlaceholders);

        // 3. Восстанавливаем слоты в виде именованных групп regex
        foreach (var (placeholder, slotName) in placeholders)
        {
            escaped = escaped.Replace(placeholder, $@"(?<{slotName}>.+?)");
        }

        return ($"^{escaped}$", slotNames);
    }

    public static bool IsProcessMatchingFilter(string filter, string activeProcessName)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true; // Глобальный пакет

        if (string.IsNullOrWhiteSpace(activeProcessName))
            return false;

        var parts = filter.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var activeClean = StripExe(activeProcessName);

        return parts.Any(p =>
        {
            var pClean = StripExe(p);
            if (string.Equals(activeClean, pClean, StringComparison.OrdinalIgnoreCase))
                return true;

            // Алиасы для Калькулятора Windows (UWP / Win32)
            if ((activeClean.Equals("calc", StringComparison.OrdinalIgnoreCase) ||
                 activeClean.Equals("calculator", StringComparison.OrdinalIgnoreCase) ||
                 activeClean.Equals("calculatorapp", StringComparison.OrdinalIgnoreCase)) &&
                (pClean.Equals("calc", StringComparison.OrdinalIgnoreCase) ||
                 pClean.Equals("calculator", StringComparison.OrdinalIgnoreCase) ||
                 pClean.Equals("calculatorapp", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        });
    }

    private static string StripExe(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return name[..^4];
        return name;
    }

    private static readonly HashSet<string> MathKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "плюс", "минус", "умножить", "разделить", "поделить", "равно",
        "ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
        "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать",
        "семнадцать", "восемнадцать", "девятнадцать", "двадцать", "тридцать", "сорок", "пятьдесят",
        "шестьдесят", "семьдесят", "восемьдесят", "девяносто", "сто", "двести", "триста", "четыреста",
        "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот", "тысяча", "миллион", "процент",
        "корень", "степень", "запятая", "точка"
    };

    /// <summary>
    /// Проверяет, содержит ли фраза цифры, математические знаки или русские числительные/операции.
    /// </summary>
    public static bool ContainsMathTokens(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return false;

        // Содержит арабские цифры или знаки +, -, *, /, =
        if (phrase.Any(c => char.IsDigit(c) || c is '+' or '-' or '*' or '/' or '=' or '%'))
            return true;

        // Содержит слова из математического словаря
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Any(w => MathKeywords.Contains(w));
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (var i = 0; i <= n; d[i, 0] = i++) { }
        for (var j = 0; j <= m; d[0, j] = j++) { }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
