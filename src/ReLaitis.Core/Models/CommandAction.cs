using System.Text.Json.Serialization;
using ReLaitis.Core.Enums;

namespace ReLaitis.Core.Models;

/// <summary>
/// Единичное действие внутри макроса голосовой команды.
/// Формат параметров читабелен для человека и LLM, с поддержкой обратной совместимости.
/// </summary>
public class CommandAction
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(ReLaitis.Core.Helpers.ActionTypeJsonConverter))]
    public ActionType Type { get; set; }

    [JsonPropertyName("parameters")]
    [JsonConverter(typeof(ReLaitis.Core.Helpers.StringArrayOrSingleJsonConverter))]
    public string[] Parameters { get; set; } = [];

    // Поддержка альтернативных параметров для LLM
    [JsonPropertyName("param")]
    public string? SingleParamFallback
    {
        get => null;
        set { if (!string.IsNullOrEmpty(value) && (Parameters == null || Parameters.Length == 0)) Parameters = [value]; }
    }



    public CommandAction() { }

    public CommandAction(ActionType type, params string[] parameters)
    {
        Type = type;
        Parameters = parameters;
    }

    [JsonIgnore]
    public string DisplayDescription => FormatDescription();

    private string FormatDescription()
    {
        return Type switch
        {
            ActionType.Hotkeys => Parameters.Length > 0 ? $"Нажать {Parameters[0]}" : "Нажать клавиши",
            ActionType.TypeText => Parameters.Length > 0 ? $"Напечатать текст {Parameters[0]}" : "Напечатать текст",
            ActionType.OpenFile => Parameters.Length > 0 ? $"Запуск: {Parameters[0]}" : "Запуск программы",
            ActionType.CloseApp => Parameters.Length > 0 ? $"Закрыть процесс: {Parameters[0]}" : "Закрыть приложение",
            ActionType.ShowWindow => Parameters.Length > 0 ? $"Окно: {Parameters[0]}" : "Управление окном",
            ActionType.Say => Parameters.Length > 0 ? $"Сказать: \"{Parameters[0]}\"" : "Озвучить",
            ActionType.PlayAudio => Parameters.Length > 0 ? $"Звук: {Path.GetFileName(Parameters[0])}" : "Воспроизвести звук",
            ActionType.Pause => Parameters.Length > 0 ? $"Пауза: {Parameters[0]} мс" : "Пауза",
            ActionType.SetVariableValue => FormatSetVariable(),
            ActionType.IfProcessSelected => Parameters.Length > 0 ? $"Если активна программа {Parameters[0]}" : "Если активна программа",
            ActionType.IfProcessExists => Parameters.Length > 0 ? $"Если запущена программа {Parameters[0]}" : "Если запущена программа",
            ActionType.IfVariableValue => FormatIfVariableValue(),
            ActionType.Loop => Parameters.Length > 0 ? $"Повторить {Parameters[0]} раз(а)" : "Цикл",
            ActionType.Break => "Прервать цикл",
            ActionType.Else => "Иначе",
            ActionType.EndBlock => "Конец блока (EndBlock)",
            ActionType.Comment => Parameters.Length > 0 ? $"// {Parameters[0]}" : "// Комментарий",
            ActionType.JetAim => "Сетка мыши JetAim",
            ActionType.Notify => Parameters.Length > 1 ? $"Уведомление: \"{Parameters[1]}\"" : "Экранное уведомление",
            ActionType.BatchScript => Parameters.Length > 0 ? $"Команда: {Parameters[0]}" : "CMD скрипт",
            ActionType.OpenURL => Parameters.Length > 0 ? $"Открыть URL: {Parameters[0]}" : "Открыть ссылку",
            ActionType.HttpWebRequest => FormatHttpWebRequest(),
            ActionType.TogglePackActivity => Parameters.Length > 0 ? $"Переключить пакет: {Parameters[0]}" : "Переключить активность пакета",
            ActionType.ScheduleEvent => FormatScheduleEvent(),
            ActionType.WaitNextPhrase => Parameters.Length > 0 ? $"Ожидать ответ -> {{{Parameters[0]}}}" : "Ожидание следующей фразы",
            ActionType.GetUrlSelectorText => Parameters.Length > 2 ? $"Получить селектор {Parameters[2]} из {Parameters[1]}" : "Текст по селектору URL",
            ActionType.While => FormatWhile(),
            ActionType.MouseMoveOn => FormatMouseMoveOn(),
            ActionType.MouseMove => Parameters.Length > 1 ? $"Мышь -> ({Parameters[0]}, {Parameters[1]})" : "Перемещение мыши",
            ActionType.MouseButton => FormatMouseButton(),
            ActionType.MouseScroll => Parameters.Length > 1 ? $"Скролл ({Parameters[1]})" : "Прокрутка колесика",
            ActionType.WebPageClick => Parameters.Length > 0 ? $"Клик в браузере: {Parameters[0]}" : "Клик в браузере",
            ActionType.WebPageNavClick => Parameters.Length > 0 ? $"Клик ссылки: {Parameters[0]}" : "Клик ссылки",
            ActionType.WebPageNavigate => Parameters.Length > 0 ? $"Перейти: {Parameters[0]}" : "Переход по адресу",
            ActionType.WebPageScript => Parameters.Length > 0 ? $"JS скрипт: {Parameters[0]}" : "Выполнить JS",
            ActionType.WebPageFocus => Parameters.Length > 0 ? $"Фокус на: {Parameters[0]}" : "Фокус в браузере",
            ActionType.WebPageGetText => Parameters.Length > 1 ? $"Текст {Parameters[0]} -> {{{Parameters[1]}}}" : "Получить текст страницы",
            ActionType.WebPagePopupOpen => Parameters.Length > 0 ? $"Вкладка браузера: {Parameters[0]}" : "Новая вкладка",
            ActionType.IfWebsiteSelected => Parameters.Length > 0 ? $"Если открыт сайт: {Parameters[0]}" : "Условие открытого сайта",
            ActionType.CSharpScript => FormatCSharpScript(),
            _ => Parameters.Length > 0 ? string.Join(", ", Parameters) : Type.ToString()
        };
    }

    private string FormatMouseMoveOn()
    {
        var target = Parameters.Length > 1 && !string.IsNullOrWhiteSpace(Parameters[1])
            ? Parameters[1]
            : (Parameters.Length > 0 && !string.IsNullOrWhiteSpace(Parameters[0]) ? Parameters[0] : string.Empty);

        return !string.IsNullOrEmpty(target)
            ? $"Навести курсор на элемент {target}"
            : "Навести курсор на элемент";
    }

    private string FormatMouseButton()
    {
        var btn = Parameters.Length > 0 ? Parameters[0] : "0";
        var btnName = btn switch
        {
            "0" or "Left" => "Левая кнопка мыши",
            "1" or "Middle" => "Средняя кнопка мыши",
            "2" or "Right" => "Правая кнопка мыши",
            _ => $"Кнопка {btn}"
        };

        var mode = Parameters.Length > 1 ? Parameters[1] : "0";
        var action = mode switch
        {
            "1" or "Down" => "Зажать",
            "2" or "Up" => "Отпустить",
            "3" or "DoubleClick" => "Дважды нажать",
            _ => "Нажать"
        };

        return $"{action} {btnName}";
    }

    private string FormatSetVariable()
    {
        if (Parameters.Length == 0) return "Задать значение переменной";
        var varName = Parameters[0];
        var formattedVar = varName.StartsWith('{') && varName.EndsWith('}') ? varName : $"{{{varName}}}";
        if (Parameters.Length == 1) return $"Задать переменной {formattedVar}";
        var value = Parameters[1];

        if (Parameters.Length >= 4 && int.TryParse(Parameters[2], out var opInt))
        {
            var op = (ArithmeticOperation)opInt;
            var opStr = op switch
            {
                ArithmeticOperation.Replace => $"Заменить на {Parameters[3]}",
                ArithmeticOperation.Add => $"+ {Parameters[3]}",
                ArithmeticOperation.Subtract => $"- {Parameters[3]}",
                ArithmeticOperation.Multiply => $"* {Parameters[3]}",
                ArithmeticOperation.Divide => $"/ {Parameters[3]}",
                ArithmeticOperation.Modulo => $"% {Parameters[3]}",
                ArithmeticOperation.Power => $"^ {Parameters[3]}",
                ArithmeticOperation.Substring => $"Подстрока {Parameters[3]}",
                _ => $"{op} {Parameters[3]}"
            };
            return $"Задать переменной {formattedVar} значение {value} {opStr}";
        }

        return $"Задать переменной {formattedVar} значение {value}";
    }

    private string FormatIfVariableValue()
    {
        if (Parameters.Length < 2) return "Условие переменной";
        var varName = Parameters[0].StartsWith('{') && Parameters[0].EndsWith('}') ? Parameters[0] : $"{{{Parameters[0]}}}";
        if (Parameters.Length == 2) return $"Если переменная {varName} = {Parameters[1]}";
        var op = Parameters[1] switch
        {
            "0" or "==" or "Equals" => "=",
            "1" or "!=" or "NotEquals" => "!=",
            "2" or ">" or "Greater" => ">",
            "3" or "<" or "Less" => "<",
            "4" or ">=" or "GreaterOrEqual" => ">=",
            "5" or "<=" or "LessOrEqual" => "<=",
            "6" or "Contains" => "содержит",
            _ => Parameters[1]
        };
        return $"Если переменная {varName} {op} {Parameters[2]}";
    }

    private string FormatWhile()
    {
        if (Parameters.Length < 2) return "Цикл While";
        var varName = Parameters[0].StartsWith('{') && Parameters[0].EndsWith('}') ? Parameters[0] : $"{{{Parameters[0]}}}";
        if (Parameters.Length == 2) return $"Пока переменная {varName} = {Parameters[1]}";
        var op = Parameters[1] switch
        {
            "0" or "==" or "Equals" => "=",
            "1" or "!=" or "NotEquals" => "!=",
            "2" or ">" or "Greater" => ">",
            "3" or "<" or "Less" => "<",
            "4" or ">=" or "GreaterOrEqual" => ">=",
            "5" or "<=" or "LessOrEqual" => "<=",
            "6" or "Contains" => "содержит",
            _ => Parameters[1]
        };
        return $"Пока переменная {varName} {op} {Parameters[2]}";
    }

    private string FormatHttpWebRequest()
    {
        if (Parameters.Length == 0) return "HTTP запрос";
        if (Parameters.Length >= 2 &&
            (Parameters[0].StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             Parameters[0].StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            var m = Parameters.Length > 1 ? Parameters[1] : "GET";
            return $"HTTP {m}: {Parameters[0]}";
        }
        var method = Parameters[0] switch
        {
            "0" => "GET",
            "1" => "POST",
            "2" => "PUT",
            "3" => "DELETE",
            "4" => "PATCH",
            _ => Parameters[0]
        };
        var url = Parameters.Length > 1 ? Parameters[1] : "";
        return $"HTTP {method}: {url}";
    }

    private string FormatScheduleEvent()
    {
        if (Parameters.Length == 0) return "Таймер команды";
        if (Parameters.Length == 2)
            return $"Таймер ({Parameters[0]} мс): \"{Parameters[1]}\"";
        if (Parameters.Length >= 3)
            return $"Событие ({Parameters[0]} {Parameters[1]}): \"{Parameters[2]}\"";
        return $"Таймер ({Parameters[0]}): \"\"";
    }

    private string FormatCSharpScript()
    {
        if (Parameters.Length == 0 || string.IsNullOrWhiteSpace(Parameters[0]))
            return "C# скрипт";

        var firstLine = Parameters[0].Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        if (firstLine.Length > 40)
            firstLine = firstLine[..37] + "...";

        return Parameters.Length > 1 && !string.IsNullOrWhiteSpace(Parameters[1])
            ? $"C# скрипт -> {{{Parameters[1]}}}: {firstLine}"
            : $"C# скрипт: {firstLine}";
    }

    public override string ToString() => DisplayDescription;
}
