using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;
using ReLaitis.Native;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ReLaitis.UI.Views;

public partial class AddActionDialog : Window
{
    private record ActionOption(string DisplayName, ActionType Type, string Param1Hint, string Param2Hint, bool IsFilePicker, string Category);

    private readonly List<ActionOption> _allOptions =
    [
        // 1. ЛОГИКА И УСЛОВИЯ
        new("Если: активно окно (IfProcessSelected)", ActionType.IfProcessSelected, "Имя процесса (например: chrome.exe, notepad.exe)", "", false, "Логика и Условия"),
        new("Если: запущен процесс (IfProcessExists)", ActionType.IfProcessExists, "Имя процесса (например: dota2.exe)", "", false, "Логика и Условия"),
        new("Если: значение переменной (IfVariableValue)", ActionType.IfVariableValue, "Имя переменной", "Оператор и значение (0 = ==, 1 = !=, 2 = >, 3 = <)", false, "Логика и Условия"),
        new("Если: открыт сайт (IfWebsiteSelected)", ActionType.IfWebsiteSelected, "Адрес или заголовок страницы (например: youtube.com)", "", false, "Логика и Условия"),
        new("Ветка: Иначе (Else)", ActionType.Else, "", "", false, "Логика и Условия"),
        new("Цикл: повторить N раз (Loop)", ActionType.Loop, "Количество повторений (например: 5)", "", false, "Логика и Условия"),
        new("Цикл: пока условие верно (While)", ActionType.While, "Имя переменной", "Оператор и значение (0 = ==, 1 = !=, 2 = >, 3 = <)", false, "Логика и Условия"),
        new("Прервать цикл (Break)", ActionType.Break, "", "", false, "Логика и Условия"),
        new("Конец блока условий/цикла (EndBlock)", ActionType.EndBlock, "", "", false, "Логика и Условия"),
        new("Случайный выбор действия (Random)", ActionType.RandomActionBlock, "", "", false, "Логика и Условия"),
        new("Установить переменную (SetVariableValue)", ActionType.SetVariableValue, "Имя переменной (например: counter, volume)", "Значение или выражение (например: 10 или {val} + 1)", false, "Логика и Условия"),
        new("Таймер / Отложенная команда (ScheduleEvent)", ActionType.ScheduleEvent, "Задержка в миллисекундах (например: 60000 = 1 мин)", "Фраза команды для запуска", false, "Логика и Условия"),
        new("Ожидать ответ пользователя (WaitNextCommand)", ActionType.WaitNextCommand, "Имя переменной для сохранения ответа", "Таймаут ожидания в мс (например: 5000)", false, "Логика и Условия"),
        new("Вкл/Выкл пакет команд (TogglePackActivity)", ActionType.TogglePackActivity, "Название пакета (например: Игры)", "Режим (0 = Выкл, 1 = Вкл, 2 = Переключить)", false, "Логика и Условия"),
        new("Выполнить голосовую команду (VoiceCommand)", ActionType.VoiceCommand, "Фраза команды для запуска", "", false, "Логика и Условия"),
        new("Заметка / Комментарий (Comment)", ActionType.Comment, "Текст заметки или комментария", "", false, "Логика и Условия"),

        // 2. КЛАВИАТУРА И МЫШЬ
        new("Навести курсор на элемент (MouseMoveOn)", ActionType.MouseMoveOn, "Название элемента на экране (кнопка, меню, текст или {название})", "Действие (опционально: 0 = клик, 2 = контекст, 3 = дабл клик)", false, "Клавиатура и Мышь"),
        new("Нажатие клавиш (Hotkeys)", ActionType.Hotkeys, "Комбинация клавиш (например: Ctrl+C, Win+D, Alt+F4, Enter)", "Действие (0 = Нажать, 1 = Зажать, 2 = Отпустить)", false, "Клавиатура и Мышь"),
        new("Ввод текста (TypeText)", ActionType.TypeText, "Текст для быстрой вставки", "", false, "Клавиатура и Мышь"),
        new("Клик кнопки мыши (MouseButton)", ActionType.MouseButton, "Кнопка (0 = Левая, 1 = Средняя, 2 = Правая)", "Действие (0 = Клик, 1 = Зажать, 2 = Отпустить)", false, "Клавиатура и Мышь"),
        new("Переместить мышь (MouseMove)", ActionType.MouseMove, "Координата X (или {x})", "Координата Y (или {y})", false, "Клавиатура и Мышь"),
        new("Прокрутка колесика (MouseScroll)", ActionType.MouseScroll, "Тип скролла (0 = Вертикальный, 1 = Горизонтальный)", "Величина (например: 120 или -120)", false, "Клавиатура и Мышь"),
        new("Координатная сетка (JetAim)", ActionType.JetAim, "", "", false, "Клавиатура и Мышь"),

        // 3. СИСТЕМА И ОКНА
        new("Запуск программы / файла (OpenFile)", ActionType.OpenFile, "Путь к приложению или файлу", "Аргументы запуска (опционально)", true, "Система и Окна"),
        new("Закрыть приложение (CloseApp)", ActionType.CloseApp, "Имя процесса (например: notepad.exe)", "Режим (0 = Закрыть окно, 1 = Принудительно убить)", false, "Система и Окна"),
        new("Управление окном (ShowWindow)", ActionType.ShowWindow, "Имя процесса", "Состояние (1 = Свернуть, 2 = Развернуть, 3 = Восстановить)", false, "Система и Окна"),
        new("Пауза / Задержка (Pause)", ActionType.Pause, "Длительность задержки в мс (например: 500)", "Тип (0 = Фиксированная, 1 = Случайная от 0 до N)", false, "Система и Окна"),
        new("Всплывающее уведомление (Notify)", ActionType.Notify, "Тип (0 = Информация, 1 = Предупреждение)", "Текст экранного уведомления", false, "Система и Окна"),
        new("Выполнить команду CMD (BatchScript)", ActionType.BatchScript, "Команда командной строки (например: shutdown /s /t 60)", "", false, "Система и Окна"),
        new("Выполнить C# код (CSharpScript)", ActionType.CSharpScript, "Код на C# (поддерживает async/await, Input, Windows, Voice, Set, Get)", "Имя переменной для return (опционально)", false, "Система и Окна"),

        // 4. АУДИО И ЗВУК
        new("Озвучить фразу (Say)", ActionType.Say, "Фраза для голосового синтезатора TTS", "", false, "Аудио и Звук"),
        new("Воспроизвести звук (PlayAudio)", ActionType.PlayAudio, "Путь к аудиофайлу (wav, mp3)", "", true, "Аудио и Звук"),

        // 5. БРАУЗЕР И СЕТЬ
        new("Открыть ссылку / поиск (OpenURL)", ActionType.OpenURL, "URL адрес (например: https://google.com/search?q={query})", "", false, "Браузер и Сеть"),
        new("HTTP вебхук / Умный дом (HttpWebRequest)", ActionType.HttpWebRequest, "URL адрес запроса (вебхук)", "Метод (GET, POST, PUT)", false, "Браузер и Сеть"),
        new("Получить текст по URL/селектору (GetUrlSelectorText)", ActionType.GetUrlSelectorText, "URL адрес веб-страницы", "CSS селектор или regex", false, "Браузер и Сеть"),
        new("Браузер: Клик по номеру/элементу (WebPageClick)", ActionType.WebPageClick, "Номер ссылки (например: 12) или CSS селектор", "", false, "Браузер и Сеть"),
        new("Браузер: Перейти по адресу (WebPageNavigate)", ActionType.WebPageNavigate, "URL адрес (например: https://yandex.ru)", "", false, "Браузер и Сеть"),
        new("Браузер: Фокус на элемент (WebPageFocus)", ActionType.WebPageFocus, "CSS селектор или имя поля ввода", "", false, "Браузер и Сеть"),
        new("Браузер: Скопировать текст страницы (WebPageGetText)", ActionType.WebPageGetText, "CSS селектор элемента", "Имя переменной для сохранения", false, "Браузер и Сеть"),
        new("Браузер: Проверка адреса (IfWebsiteNavValue)", ActionType.IfWebsiteNavValue, "Часть URL адреса", "", false, "Браузер и Сеть"),
        new("Браузер: Выполнить JavaScript (WebPageScript)", ActionType.WebPageScript, "JS код", "", false, "Браузер и Сеть"),
        new("Браузер: Управление вкладками (WebPagePopupOpen)", ActionType.WebPagePopupOpen, "Действие (new, close, next, prev, reload, back, forward)", "", false, "Браузер и Сеть")
    ];

    private static readonly string[] Categories =
    [
        "Все действия",
        "Логика и Условия",
        "Клавиатура и Мышь",
        "Система и Окна",
        "Аудио и Звук",
        "Браузер и Сеть"
    ];

    private readonly CommandAction? _actionToEdit;
    private List<ActionOption> _currentOptions = [];

    public CommandAction? CreatedAction { get; private set; }
    public List<CommandAction> CreatedActions { get; } = [];

    public AddActionDialog(CommandAction? actionToEdit = null, ActionType? initialType = null)
    {
        InitializeComponent();
        _actionToEdit = actionToEdit;

        CategoryComboBox.ItemsSource = Categories;

        if (_actionToEdit != null)
        {
            DialogTitle.Text = "Редактировать действие";
            Title = "Редактировать действие";
            SaveBtn.Content = "Сохранить";

            var opt = _allOptions.FirstOrDefault(o => o.Type == _actionToEdit.Type);
            var category = opt?.Category ?? "Все действия";
            var catIdx = Array.IndexOf(Categories, category);
            CategoryComboBox.SelectedIndex = catIdx >= 0 ? catIdx : 0;

            var optionIndex = _currentOptions.FindIndex(o => o.Type == _actionToEdit.Type);
            ActionTypeComboBox.SelectedIndex = optionIndex >= 0 ? optionIndex : 0;

            if (_actionToEdit.Type == ActionType.MouseMoveOn)
            {
                var target = _actionToEdit.Parameters.Length > 1 && !string.IsNullOrWhiteSpace(_actionToEdit.Parameters[1])
                    ? _actionToEdit.Parameters[1]
                    : (_actionToEdit.Parameters.Length > 0 ? _actionToEdit.Parameters[0] : "");
                var subAction = _actionToEdit.Parameters.Length > 1 && !string.IsNullOrWhiteSpace(_actionToEdit.Parameters[1])
                    ? _actionToEdit.Parameters[0]
                    : "";

                Param1Box.Text = target;
                Param2Box.Text = subAction;
            }
            else if (_actionToEdit.Type == ActionType.CSharpScript)
            {
                if (_actionToEdit.Parameters.Length > 0)
                {
                    ParamCodeBox.Text = _actionToEdit.Parameters[0];
                    Param1Box.Text = _actionToEdit.Parameters[0];
                }
                if (_actionToEdit.Parameters.Length > 1)
                    Param2Box.Text = _actionToEdit.Parameters[1];
            }
            else
            {
                if (_actionToEdit.Parameters.Length > 0)
                    Param1Box.Text = _actionToEdit.Parameters[0];

                if (_actionToEdit.Parameters.Length > 1)
                    Param2Box.Text = _actionToEdit.Parameters[1];
            }
        }
        else if (initialType.HasValue)
        {
            var opt = _allOptions.FirstOrDefault(o => o.Type == initialType.Value);
            var category = opt?.Category ?? "Логика и Условия";
            var catIdx = Array.IndexOf(Categories, category);
            CategoryComboBox.SelectedIndex = catIdx >= 0 ? catIdx : 0;

            var optionIndex = _currentOptions.FindIndex(o => o.Type == initialType.Value);
            ActionTypeComboBox.SelectedIndex = optionIndex >= 0 ? optionIndex : 0;
        }
        else
        {
            CategoryComboBox.SelectedIndex = 0;
        }
    }

    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var cat = CategoryComboBox.SelectedItem as string ?? "Все действия";
        if (cat == "Все действия")
        {
            _currentOptions = [.. _allOptions];
        }
        else
        {
            _currentOptions = _allOptions.Where(o => o.Category == cat).ToList();
        }

        ActionTypeComboBox.ItemsSource = _currentOptions.Select(o => o.DisplayName).ToList();
        if (_currentOptions.Count > 0)
            ActionTypeComboBox.SelectedIndex = 0;
    }

    private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var idx = ActionTypeComboBox.SelectedIndex;
        if (idx < 0 || idx >= _currentOptions.Count) return;

        var opt = _currentOptions[idx];

        Param1Label.Text = opt.Param1Hint;
        Param1Label.Visibility = string.IsNullOrEmpty(opt.Param1Hint) ? Visibility.Collapsed : Visibility.Visible;

        var isCSharp = opt.Type == ActionType.CSharpScript;
        if (isCSharp)
        {
            Param1SingleLineGrid.Visibility = Visibility.Collapsed;
            ParamCodeBox.Visibility = Visibility.Visible;
            if (string.IsNullOrEmpty(ParamCodeBox.Text) && !string.IsNullOrEmpty(Param1Box.Text))
            {
                ParamCodeBox.Text = Param1Box.Text;
            }
            else if (string.IsNullOrEmpty(ParamCodeBox.Text))
            {
                ParamCodeBox.Text = "// Доступны: Input, Windows, Voice, Set(\"var\", val), Get(\"var\"), Say(\"текст\"), await Delay(100);\nSay(\"Привет из C#!\");";
            }
            HintTextBlock.Text = "Подсказка: в C# скрипте доступны объекты Context, Input, Windows, Voice, методы Get(name), Set(name, val), Say(text), Notify(text), RunCommand(phrase) и ключевое слово return.";
        }
        else
        {
            Param1SingleLineGrid.Visibility = Param1Label.Visibility;
            ParamCodeBox.Visibility = Visibility.Collapsed;
            HintTextBlock.Text = "Подсказка: можно использовать переменные, например {ActiveProcess}, {Time} или {переменная}.";
        }

        OpenCodeEditorBtn.Visibility = isCSharp ? Visibility.Visible : Visibility.Collapsed;

        BrowseFileBtn.Visibility = opt.IsFilePicker ? Visibility.Visible : Visibility.Collapsed;

        var isProcessCondition = opt.Type is ActionType.IfProcessSelected or ActionType.IfProcessExists;
        PickActiveProcessBtn.Visibility = isProcessCondition ? Visibility.Visible : Visibility.Collapsed;

        Param2Label.Text = opt.Param2Hint;
        Param2Label.Visibility = string.IsNullOrEmpty(opt.Param2Hint) ? Visibility.Collapsed : Visibility.Visible;
        Param2Box.Visibility = Param2Label.Visibility;

        var isCondition = opt.Type is ActionType.IfProcessSelected
            or ActionType.IfProcessExists
            or ActionType.IfVariableValue
            or ActionType.IfWebsiteSelected;

        var isLoop = opt.Type == ActionType.Loop;

        if (_actionToEdit == null && (isCondition || isLoop))
        {
            LogicBlockOptionsPanel.Visibility = Visibility.Visible;
            AutoElseCheckBox.Visibility = isCondition ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            LogicBlockOptionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OpenCodeEditorBtn_Click(object sender, RoutedEventArgs e) => OpenCSharpEditor();

    private void ParamCodeBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenCSharpEditor();

    private void OpenCSharpEditor()
    {
        var editor = new CSharpEditorWindow(ParamCodeBox.Text, Param2Box.Text)
        {
            Owner = this
        };

        if (editor.ShowDialog() == true)
        {
            ParamCodeBox.Text = editor.ResultCode;
            if (!string.IsNullOrEmpty(editor.ResultTargetVar))
            {
                Param2Box.Text = editor.ResultTargetVar;
            }
        }
    }

    private void PickActiveProcessBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var wm = new WindowsWindowManager();
            var proc = wm.GetActiveProcessName();
            if (!string.IsNullOrWhiteSpace(proc))
            {
                if (!proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    proc += ".exe";
                Param1Box.Text = proc;
            }
        }
        catch
        {
            // fallback
        }
    }

    private void BrowseFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Выберите файл",
            Filter = "Все файлы (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            Param1Box.Text = dialog.FileName;
        }
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        var idx = ActionTypeComboBox.SelectedIndex;
        if (idx < 0 || idx >= _currentOptions.Count) return;

        var opt = _currentOptions[idx];
        var p1 = (opt.Type == ActionType.CSharpScript ? ParamCodeBox.Text : Param1Box.Text)?.Trim() ?? "";
        var p2 = Param2Box.Text?.Trim() ?? "";

        var paramsList = new List<string>();

        if (opt.Type == ActionType.MouseMoveOn)
        {
            // Формат Laitis: P[0] = действие (или пусто), P[1] = имя элемента
            paramsList.Add(p2);
            paramsList.Add(p1);
        }
        else if (opt.Type == ActionType.SetVariableValue && _actionToEdit != null && _actionToEdit.Parameters.Length >= 4)
        {
            // Сохраняем расширенные параметры Laitis (операция и операнд замены)
            paramsList.Add(p1);
            paramsList.Add(p2);
            for (var i = 2; i < _actionToEdit.Parameters.Length; i++)
            {
                paramsList.Add(_actionToEdit.Parameters[i]);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(p1) || !string.IsNullOrEmpty(p2))
            {
                paramsList.Add(p1);
                if (!string.IsNullOrEmpty(p2))
                    paramsList.Add(p2);
            }
        }

        CreatedActions.Clear();

        if (_actionToEdit != null)
        {
            _actionToEdit.Type = opt.Type;
            _actionToEdit.Parameters = paramsList.ToArray();
            CreatedAction = _actionToEdit;
            CreatedActions.Add(_actionToEdit);
        }
        else
        {
            var mainAction = new CommandAction(opt.Type, paramsList.ToArray());
            CreatedAction = mainAction;
            CreatedActions.Add(mainAction);

            var isCondition = opt.Type is ActionType.IfProcessSelected
                or ActionType.IfProcessExists
                or ActionType.IfVariableValue
                or ActionType.IfWebsiteSelected;

            var isLoop = opt.Type is ActionType.Loop or ActionType.While;

            if (LogicBlockOptionsPanel.Visibility == Visibility.Visible && AutoEndBlockCheckBox.IsChecked == true)
            {
                if (isCondition && AutoElseCheckBox.IsChecked == true)
                {
                    CreatedActions.Add(new CommandAction(ActionType.Else));
                }
                CreatedActions.Add(new CommandAction(ActionType.EndBlock));
            }
        }

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
