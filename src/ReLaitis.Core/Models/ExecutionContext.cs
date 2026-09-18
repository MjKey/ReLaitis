using System.Text.RegularExpressions;
using ReLaitis.Core.Engine;
using ReLaitis.Core.Interfaces;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Models;

/// <summary>
/// Контекст исполнения действий голосовой команды.
/// Содержит сервисы ввода, окна, переменные и состояние выполнения.
/// </summary>
public class MacroExecutionContext
{
    public IInputSimulator Input { get; }
    public IWindowManager Windows { get; }
    public IVoiceFeedback Voice { get; }
    public IBrowserBridge? BrowserBridge { get; init; }

    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string>? _globalVariablesRef;

    public double ScreenWidth { get; set; } = 1920;
    public double ScreenHeight { get; set; } = 1080;

    public bool ShouldBreak { get; set; }
    public CancellationToken CancellationToken { get; }

    public event Action<string, string>? NotificationTriggered;
    public event Action<string>? VoiceCommandRequested;
    public event Action<string, int>? TogglePackRequested;
    public event Action<int, string>? ScheduleEventRequested;
    public event Action? JetAimRequested;
    public Func<int, Task<string?>>? WaitForNextPhraseAsync;

    public MacroExecutionContext(
        IInputSimulator input,
        IWindowManager windows,
        IVoiceFeedback voice,
        Dictionary<string, string>? initialVariables = null,
        CancellationToken cancellationToken = default,
        IBrowserBridge? browserBridge = null,
        Dictionary<string, string>? globalVariablesRef = null)
    {
        Input = input;
        Windows = windows;
        Voice = voice;
        BrowserBridge = browserBridge;
        CancellationToken = cancellationToken;
        _globalVariablesRef = globalVariablesRef;

        if (globalVariablesRef != null)
        {
            foreach (var (k, v) in globalVariablesRef)
            {
                Variables[k] = v;
            }
        }

        if (initialVariables != null)
        {
            foreach (var (k, v) in initialVariables)
            {
                Variables[k] = v;
            }
        }
    }

    /// <summary>
    /// Получает значение переменной по имени, или null, если не найдена.
    /// </summary>
    public string? GetVariable(string name)
    {
        var cleanName = name.Trim().TrimStart('{').TrimEnd('}');
        return Variables.TryGetValue(cleanName, out var val) ? val : null;
    }

    /// <summary>
    /// Записывает значение переменной. Если переменная является общей (глобальной),
    /// обновляет также общее хранилище переменных.
    /// </summary>
    public void SetVariable(string name, string value)
    {
        var cleanName = name.Trim().TrimStart('{').TrimEnd('}');
        Variables[cleanName] = value;
        if (_globalVariablesRef != null)
        {
            _globalVariablesRef[cleanName] = value;
        }
    }

    public void TriggerNotification(string title, string message) =>
        NotificationTriggered?.Invoke(title, message);

    public void RequestVoiceCommand(string commandPhrase) =>
        VoiceCommandRequested?.Invoke(commandPhrase);

    public void RequestTogglePack(string packName, int state = 2) =>
        TogglePackRequested?.Invoke(packName, state);

    public void RequestScheduleEvent(int delayMs, string phrase) =>
        ScheduleEventRequested?.Invoke(delayMs, phrase);

    public void RequestJetAim() =>
        JetAimRequested?.Invoke();

    /// <summary>
    /// Разрешает переменные внутри строки, например "Привет, {username}!" или "Случайное: {rnd:1:100}".
    /// Поддерживает системные переменные: {Clipboard}, {ActiveProcess}, {ActiveTitle}, {Time}, {Date}, {DateTime}, {UserName}, {ComputerName},
    /// а также динамические токены случайных чисел {rnd:min:max}, {rnd:max}, {random:min:max}.
    /// </summary>
    public string ResolveVariables(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('{'))
            return input;

        var result = input;

        // 1. Системные переменные процессов и окон
        if (result.Contains("{ActiveProcess}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{ActiveProcess}", Windows.GetActiveProcessName());

        if (result.Contains("{active_process}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{active_process}", Windows.GetActiveProcessName());

        if (result.Contains("{ActiveTitle}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{ActiveTitle}", Windows.GetActiveWindowTitle());

        if (result.Contains("{active_window}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{active_window}", Windows.GetActiveWindowTitle());

        if (result.Contains("{active_title}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{active_title}", Windows.GetActiveWindowTitle());

        // 2. Системные дата и время
        if (result.Contains("{DateTime}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        if (result.Contains("{Time}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{Time}", DateTime.Now.ToString("HH:mm:ss"));

        if (result.Contains("{Date}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{Date}", DateTime.Now.ToString("yyyy-MM-dd"));

        // 3. Буфер обмена
        if (result.Contains("{Clipboard}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{Clipboard}", Windows.GetClipboardText());

        // 4. Окружение пользователя и ПК
        if (result.Contains("{UserName}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{UserName}", Environment.UserName);

        if (result.Contains("{ComputerName}", StringComparison.OrdinalIgnoreCase))
            result = ReplaceTokenIgnoreCase(result, "{ComputerName}", Environment.MachineName);

        // 5. Браузер
        if (BrowserBridge != null)
        {
            if (result.Contains("{BrowserUrl}", StringComparison.OrdinalIgnoreCase))
                result = ReplaceTokenIgnoreCase(result, "{BrowserUrl}", BrowserBridge.CurrentUrl ?? "");

            if (result.Contains("{browser_url}", StringComparison.OrdinalIgnoreCase))
                result = ReplaceTokenIgnoreCase(result, "{browser_url}", BrowserBridge.CurrentUrl ?? "");

            if (result.Contains("{BrowserTitle}", StringComparison.OrdinalIgnoreCase))
                result = ReplaceTokenIgnoreCase(result, "{BrowserTitle}", BrowserBridge.CurrentTitle ?? "");

            if (result.Contains("{browser_title}", StringComparison.OrdinalIgnoreCase))
                result = ReplaceTokenIgnoreCase(result, "{browser_title}", BrowserBridge.CurrentTitle ?? "");
        }

        // 6. Пользовательские переменные
        foreach (var (key, value) in Variables)
        {
            var placeholder = "{" + key + "}";
            if (result.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                result = ReplaceTokenIgnoreCase(result, placeholder, value);
            }
        }

        // 7. Случайные числа {rnd:min:max}, {rnd:max}, {random:min:max}
        // Запускаем как до, так и после пользовательских переменных,
        // чтобы если переменная содержала токен {rnd:...}, он тоже раскрылся в число.
        result = ResolveRandomTokens(result);

        return result;
    }

    private static string ReplaceTokenIgnoreCase(string input, string pattern, string replacement)
    {
        return Regex.Replace(input, Regex.Escape(pattern), replacement ?? string.Empty, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Преобразует динамические токены {rnd:min:max}, {rnd:max}, {random:min:max}, {rnd} в псевдослучайные целые числа.
    /// </summary>
    public static string ResolveRandomTokens(string input) => VariableCalculator.ResolveRandomTokens(input);
}
