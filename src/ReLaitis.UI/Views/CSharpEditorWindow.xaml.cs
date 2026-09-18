using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReLaitis.Core.Engine;
using ReLaitis.Core.Interfaces;
using ReLaitis.Core.Models;
using ReLaitis.Native;
using MediaColor = System.Windows.Media.Color;

namespace ReLaitis.UI.Views;

public partial class CSharpEditorWindow : Window
{
    public string ResultCode { get; private set; } = string.Empty;
    public string ResultTargetVar { get; private set; } = string.Empty;

    public CSharpEditorWindow(string initialCode = "", string initialTargetVar = "")
    {
        InitializeComponent();

        if (string.IsNullOrWhiteSpace(initialCode))
        {
            CodeEditorBox.Text =
@"// Доступные объекты: Input, Windows, Voice, Context, CancellationToken
// Методы-хелперы: Get(var), Set(var, val), Say(text), Notify(text), RunCommand(cmd), await Delay(ms)

var activeTitle = Windows.GetActiveWindowTitle();
Say($""Активное окно: {activeTitle}"");

return $""Проверено в {DateTime.Now:HH:mm:ss}"";";
        }
        else
        {
            CodeEditorBox.Text = initialCode;
        }

        ResultVarBox.Text = initialTargetVar ?? string.Empty;
        UpdateCursorPosition();
    }

    private void CheckSyntaxBtn_Click(object sender, RoutedEventArgs e)
    {
        var code = CodeEditorBox.Text;
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusBadge.Text = "Код пуст";
            StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
            LogToConsole("Скрипт пуст.");
            return;
        }

        StatusBadge.Text = "Компиляция...";
        StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));

        var isValid = CSharpScriptEngine.Validate(code, out var errors);

        if (isValid)
        {
            StatusBadge.Text = "Синтаксис корректен";
            StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(52, 211, 153));
            LogToConsole($"[{DateTime.Now:HH:mm:ss}] Компиляция успешна. Ошибок не обнаружено. Скрипт готов к запуску.");
        }
        else
        {
            StatusBadge.Text = $"Ошибок: {errors.Count}";
            StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));

            LogToConsole($"[{DateTime.Now:HH:mm:ss}] Обнаружены ошибки компиляции ({errors.Count}):");
            foreach (var err in errors)
            {
                LogToConsole($"  • {err}");
            }
        }
    }

    private async void TestRunBtn_Click(object sender, RoutedEventArgs e)
    {
        var code = CodeEditorBox.Text;
        if (string.IsNullOrWhiteSpace(code))
        {
            LogToConsole("Невозможно запустить: код пуст.");
            return;
        }

        TestRunBtn.IsEnabled = false;
        StatusBadge.Text = "Выполнение...";
        StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(251, 191, 36));

        LogToConsole($"[{DateTime.Now:HH:mm:ss}] ▶ Запуск тестового выполнения C# скрипта...");

        try
        {
            // Тестовое окружение с перехватом событий
            var inputSim = new WindowsInputSimulator();
            var winMgr = new WindowsWindowManager();
            var testVoice = new TestEditorVoiceFeedback(msg => LogToConsole($"  [Say (TTS)]: \"{msg}\""));

            var testContext = new MacroExecutionContext(inputSim, winMgr, testVoice);
            testContext.NotificationTriggered += (title, text) =>
                LogToConsole($"  [Notify]: \"{title}\" - {text}");
            testContext.VoiceCommandRequested += phrase =>
                LogToConsole($"  [RunCommand]: \"{phrase}\"");

            var globals = new CSharpScriptGlobals(testContext);

            var sw = Stopwatch.StartNew();
            var result = await CSharpScriptEngine.ExecuteAsync(code, globals);
            sw.Stop();

            StatusBadge.Text = "Выполнено успешно";
            StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(52, 211, 153));

            LogToConsole($"[{DateTime.Now:HH:mm:ss}] Успешно завершено за {sw.ElapsedMilliseconds} мс.");

            if (result != null)
            {
                LogToConsole($"  [Результат (return)]: {result}");
                var targetVar = ResultVarBox.Text.Trim();
                if (!string.IsNullOrEmpty(targetVar))
                {
                    LogToConsole($"  [Переменная {{{targetVar}}}]: будет установлено значение \"{result}\"");
                }
            }
            else
            {
                LogToConsole("  [Результат (return)]: null / без возвращаемого значения");
            }

            if (testContext.Variables.Count > 0)
            {
                LogToConsole("  [Измененные переменные]:");
                foreach (var (k, v) in testContext.Variables)
                {
                    LogToConsole($"    • {{{k}}} = \"{v}\"");
                }
            }
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException ex)
        {
            StatusBadge.Text = "Ошибка компиляции";
            StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));
            LogToConsole($"[{DateTime.Now:HH:mm:ss}] Ошибка компиляции:");
            foreach (var d in ex.Diagnostics)
            {
                LogToConsole($"  • Строка {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}");
            }
        }
        catch (Exception ex)
        {
            StatusBadge.Text = "Исключение";
            StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));
            LogToConsole($"[{DateTime.Now:HH:mm:ss}] Ошибка времени выполнения ({ex.GetType().Name}): {ex.Message}");
        }
        finally
        {
            TestRunBtn.IsEnabled = true;
        }
    }

    private void SnippetsMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        menu.Items.Add(CreateSnippetItem("Озвучить фразу (Say)",
@"Say(""Голосовой ассистент на связи!"");"));

        menu.Items.Add(CreateSnippetItem("Нажать сочетание клавиш (Input.SendHotkey)",
@"Input.SendHotkey(""Ctrl+C"");"));

        menu.Items.Add(CreateSnippetItem("Ввести текст (Input.TypeText)",
@"Input.TypeText(""Привет из C# скрипта!"");"));

        menu.Items.Add(new Separator());

        menu.Items.Add(CreateSnippetItem("Активное окно и процесс (Windows)",
@"var proc = Windows.GetActiveProcessName();
var title = Windows.GetActiveWindowTitle();
Say($""Активно окно {proc}: {title}"");"));

        menu.Items.Add(CreateSnippetItem("Буфер обмена (Windows.GetClipboardText)",
@"var clip = Windows.GetClipboardText();
if (!string.IsNullOrEmpty(clip))
{
    Notify($""Длина буфера: {clip.Length} симв."");
}"));

        menu.Items.Add(new Separator());

        menu.Items.Add(CreateSnippetItem("Чтение и запись переменных (Get / Set)",
@"var counterStr = Get(""counter"") ?? ""0"";
var nextVal = int.Parse(counterStr) + 1;
Set(""counter"", nextVal);
Say($""Счетчик равен {nextVal}"");"));

        menu.Items.Add(CreateSnippetItem("HTTP GET запрос (HttpClient)",
@"using var client = new HttpClient();
var response = await client.GetStringAsync(""https://api.ipify.org"");
Set(""external_ip"", response);
Say($""Ваш IP: {response}"");
return response;"));

        menu.Items.Add(CreateSnippetItem("Асинхронная пауза (await Delay)",
@"await Delay(1000);
Say(""Прошла одна секунда"");"));

        menu.Items.Add(CreateSnippetItem("Запустить другую команду (RunCommand)",
@"RunCommand(""открой блокнот"");"));

        menu.PlacementTarget = SnippetsMenuBtn;
        menu.IsOpen = true;
    }

    private MenuItem CreateSnippetItem(string title, string snippetCode)
    {
        var item = new MenuItem { Header = title };
        item.Click += (_, _) =>
        {
            InsertSnippet(snippetCode);
        };
        return item;
    }

    private void InsertSnippet(string snippet)
    {
        var idx = CodeEditorBox.CaretIndex;
        if (idx < 0 || idx > CodeEditorBox.Text.Length)
            idx = CodeEditorBox.Text.Length;

        var prefix = (idx > 0 && !CodeEditorBox.Text[..idx].EndsWith(Environment.NewLine))
            ? Environment.NewLine
            : "";

        CodeEditorBox.Text = CodeEditorBox.Text.Insert(idx, prefix + snippet + Environment.NewLine);
        CodeEditorBox.CaretIndex = idx + prefix.Length + snippet.Length + Environment.NewLine.Length;
        CodeEditorBox.Focus();
    }

    private void ClearConsoleBtn_Click(object sender, RoutedEventArgs e)
    {
        ConsoleBox.Clear();
        StatusBadge.Text = "Готов к проверке";
        StatusBadge.Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
    }

    private void LogToConsole(string text)
    {
        ConsoleBox.AppendText(text + Environment.NewLine);
        ConsoleBox.ScrollToEnd();
    }

    private void CodeEditorBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateCursorPosition();
    }

    private void UpdateCursorPosition()
    {
        if (CursorPosText == null || CodeEditorBox == null) return;

        var caret = CodeEditorBox.CaretIndex;
        var text = CodeEditorBox.Text;

        var line = 1;
        var col = 1;
        for (var i = 0; i < caret && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else if (text[i] != '\r')
            {
                col++;
            }
        }

        CursorPosText.Text = $"Стр: {line}, Кол: {col}";
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        ResultCode = CodeEditorBox.Text;
        ResultTargetVar = ResultVarBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private class TestEditorVoiceFeedback : IVoiceFeedback
    {
        private readonly Action<string> _onSpeak;

        public TestEditorVoiceFeedback(Action<string> onSpeak)
        {
            _onSpeak = onSpeak;
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            _onSpeak(text);
            return Task.CompletedTask;
        }

        public Task PlayAudioAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            _onSpeak($"[Аудиофайл]: {audioFilePath}");
            return Task.CompletedTask;
        }

        public void StopSpeaking() { }
    }
}
