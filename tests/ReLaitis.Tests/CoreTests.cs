using ReLaitis.Audio;
using ReLaitis.Audio.Synthesis;
using ReLaitis.Core.Engine;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Interfaces;
using ReLaitis.Core.Models;
using ReLaitis.Core.Network;
using ReLaitis.Core.Storage;
using ReLaitis.Native;
using Xunit;

namespace ReLaitis.Tests;

public class MockInputSimulator : IInputSimulator
{
    public List<string> SentHotkeys { get; } = [];
    public List<string> TypedTexts { get; } = [];
    public List<(int X, int Y)> MouseMoves { get; } = [];
    public List<int> MouseClicks { get; } = [];

    public void SendHotkey(string keyCombination, ButtonAction action = ButtonAction.Press) =>
        SentHotkeys.Add($"{keyCombination}:{action}");

    public void TypeText(string text) => TypedTexts.Add(text);

    public void MoveMouse(int x, int y, MouseMoveType moveType, string? scribbleCoords = null) =>
        MouseMoves.Add((x, y));

    public void MouseClick(int button, ButtonAction action = ButtonAction.Press) =>
        MouseClicks.Add(button);

    public void MouseScroll(MouseScrollType scrollType, int delta) { }
    public (int X, int Y) GetMousePosition() => (100, 100);
}

public class MockWindowManager : IWindowManager
{
    public string ActiveProcess { get; set; } = "explorer.exe";
    public string ActiveTitle { get; set; } = "Desktop";
    public List<string> StartedProcesses { get; } = [];

    public string GetActiveProcessName() => ActiveProcess;
    public string GetActiveWindowTitle() => ActiveTitle;
    public bool IsProcessRunning(string processName) => true;
    public bool IsProcessActive(string processName) => string.IsNullOrWhiteSpace(processName) || string.Equals(ActiveProcess, processName, StringComparison.OrdinalIgnoreCase);
    public bool StartProcess(string fileName, string arguments = "")
    {
        StartedProcesses.Add($"{fileName} {arguments}".Trim());
        return true;
    }
    public bool CloseProcess(string processName, CloseAppBehaviour behaviour = CloseAppBehaviour.Close) => true;
    public bool SetWindowState(string processName, ShowWindowCommandType command) => true;
    public string ClipboardText { get; set; } = "";
    public bool FindAndClickElementByName(string elementName, string actionName = "") => true;
    public string GetClipboardText() => ClipboardText;
}

public class MockVoiceFeedback : IVoiceFeedback
{
    public List<string> SpokenPhrases { get; } = [];

    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        SpokenPhrases.Add(text);
        return Task.CompletedTask;
    }

    public Task PlayAudioAsync(string audioFilePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void StopSpeaking() { }
}

public class MockBrowserBridge : IBrowserBridge
{
    public bool IsConnected { get; set; } = true;
    public string? CurrentUrl { get; set; } = "https://example.com/page";
    public string? CurrentTitle { get; set; } = "Example Domain";

    public List<string> ClickedTargets { get; } = [];
    public List<string> NavigatedUrls { get; } = [];
    public List<string> ExecutedScripts { get; } = [];
    public List<(string Direction, int Amount)> Scrolls { get; } = [];
    public List<bool> HintsToggled { get; } = [];
    public List<string> TabCommands { get; } = [];
    public string TextToReturn { get; set; } = "Mock Element Text";

#pragma warning disable CS0067
    public event Action<bool>? ConnectionChanged;
    public event Action<string, string>? PageStateChanged;
#pragma warning restore CS0067

    public Task<bool> ClickElementAsync(string target, CancellationToken ct = default)
    {
        ClickedTargets.Add(target);
        return Task.FromResult(true);
    }

    public Task<bool> NavigateAsync(string url, CancellationToken ct = default)
    {
        NavigatedUrls.Add(url);
        return Task.FromResult(true);
    }

    public Task<bool> ScrollAsync(string direction, int amount = 400, CancellationToken ct = default)
    {
        Scrolls.Add((direction, amount));
        return Task.FromResult(true);
    }

    public Task<bool> ExecuteScriptAsync(string code, CancellationToken ct = default)
    {
        ExecutedScripts.Add(code);
        return Task.FromResult(true);
    }

    public Task<bool> ToggleHintsAsync(bool show, CancellationToken ct = default)
    {
        HintsToggled.Add(show);
        return Task.FromResult(true);
    }

    public Task<string?> GetElementTextAsync(string selector, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(TextToReturn);
    }

    public Task<bool> TabActionAsync(string command, CancellationToken ct = default)
    {
        TabCommands.Add(command);
        return Task.FromResult(true);
    }
}

public class CoreTests
{
    [Fact]
    public void VariableCalculator_ArithmeticOperations_WorkCorrectly()
    {
        Assert.Equal("15", VariableCalculator.Calculate("10", ArithmeticOperation.Add, "5"));
        Assert.Equal("5", VariableCalculator.Calculate("10", ArithmeticOperation.Subtract, "5"));
        Assert.Equal("50", VariableCalculator.Calculate("10", ArithmeticOperation.Multiply, "5"));
        Assert.Equal("2", VariableCalculator.Calculate("10", ArithmeticOperation.Divide, "5"));
        Assert.Equal("1", VariableCalculator.Calculate("10", ArithmeticOperation.Modulo, "3"));
        Assert.Equal("8", VariableCalculator.Calculate("2", ArithmeticOperation.Power, "3"));
        Assert.Equal("4", VariableCalculator.Calculate("16", ArithmeticOperation.Sqrt, ""));
        Assert.Equal("12.35", VariableCalculator.Calculate("12.3456", ArithmeticOperation.Round, "2"));
    }

    [Fact]
    public void VariableCalculator_StringAndConditions_WorkCorrectly()
    {
        Assert.Equal("Hello World", VariableCalculator.Calculate("Hello User", ArithmeticOperation.Replace, "User|World"));

        Assert.True(VariableCalculator.EvaluateCondition("10", VariableOperator.Greater, "5"));
        Assert.True(VariableCalculator.EvaluateCondition("abc", VariableOperator.Equals, "abc"));
        Assert.True(VariableCalculator.EvaluateCondition("discord.exe", VariableOperator.Contains, "discord"));
        Assert.True(VariableCalculator.EvaluateCondition("10", VariableOperator.LessOrEqual, "10"));
        Assert.False(VariableCalculator.EvaluateCondition("5", VariableOperator.Greater, "10"));
    }

    [Fact]
    public void VariableCalculator_EvaluateExpression_And_RandomTokens_WorkCorrectly()
    {
        // 1. Math expressions
        Assert.Equal("15", VariableCalculator.EvaluateExpression("10 + 5"));
        Assert.Equal("2", VariableCalculator.EvaluateExpression("3 - 1"));
        Assert.Equal("24", VariableCalculator.EvaluateExpression("6 * 4"));
        Assert.Equal("5", VariableCalculator.EvaluateExpression("20 / 4"));
        Assert.Equal("1", VariableCalculator.EvaluateExpression("10 % 3"));
        Assert.Equal("16", VariableCalculator.EvaluateExpression("(2 + 2) * 4"));

        // 2. Random tokens
        var rndStr = VariableCalculator.ResolveRandomTokens("{rnd:1:100}");
        Assert.True(int.TryParse(rndStr, out var rndVal));
        Assert.InRange(rndVal, 1, 100);

        var rndSingle = VariableCalculator.ResolveRandomTokens("{rnd:50}");
        Assert.True(int.TryParse(rndSingle, out var rndSingleVal));
        Assert.InRange(rndSingleVal, 1, 50);

        var randomStr = VariableCalculator.ResolveRandomTokens("{random:10:20}");
        Assert.True(int.TryParse(randomStr, out var randomVal));
        Assert.InRange(randomVal, 10, 20);

        // 3. String that is not math expression
        Assert.Equal("notepad.exe", VariableCalculator.EvaluateExpression("notepad.exe"));
    }

    [Fact]
    public void MacroExecutionContext_ResolveVariables_RandomAndSystemVariables_ResolveCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager
        {
            ActiveProcess = "Code.exe",
            ActiveTitle = "Visual Studio Code",
            ClipboardText = "Sample Clipboard Text"
        };
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);

        // System variables
        var resolvedProc = context.ResolveVariables("Процесс: {active_process}");
        Assert.Equal("Процесс: Code.exe", resolvedProc);

        var resolvedTitle = context.ResolveVariables("Окно: {active_window}");
        Assert.Equal("Окно: Visual Studio Code", resolvedTitle);

        var resolvedClip = context.ResolveVariables("Буфер: {clipboard}");
        Assert.Equal("Буфер: Sample Clipboard Text", resolvedClip);

        // Random tokens in text
        var resolvedRnd = context.ResolveVariables("Выпало число {rnd:1:100}");
        Assert.DoesNotContain("{rnd:1:100}", resolvedRnd);
        Assert.StartsWith("Выпало число ", resolvedRnd);
        var numStr = resolvedRnd["Выпало число ".Length..];
        Assert.True(int.TryParse(numStr, out var n));
        Assert.InRange(n, 1, 100);
    }

    [Fact]
    public async Task ActionExecutor_RandomNumberCommand_SubstitutesActualNumberAndSpeaksIt()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);
        var executor = new ActionExecutor();

        // Testing the exact command from user's case:
        // Action 1: SetVariableValue "randVal", "{rnd:1:100}"
        // Action 2: Say "Выпало число {randVal}"
        var actions = new List<CommandAction>
        {
            new(ActionType.SetVariableValue, "randVal", "{rnd:1:100}"),
            new(ActionType.Say, "Выпало число {randVal}")
        };

        await executor.ExecuteAsync(actions, context);

        Assert.Single(voice.SpokenPhrases);
        var spoken = voice.SpokenPhrases[0];
        Assert.DoesNotContain("{rnd:1:100}", spoken);
        Assert.DoesNotContain("{randVal}", spoken);
        Assert.StartsWith("Выпало число ", spoken);

        var numStr = spoken["Выпало число ".Length..];
        Assert.True(int.TryParse(numStr, out var rolledNum));
        Assert.InRange(rolledNum, 1, 100);
    }

    [Fact]
    public async Task ActionExecutor_MathCalculatorAndLoopDecrement_EvaluatesExpressionsCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);
        var executor = new ActionExecutor();

        // 1. Calculator: sum = {x} + {y}
        context.SetVariable("x", "12");
        context.SetVariable("y", "8");
        var calcActions = new List<CommandAction>
        {
            new(ActionType.SetVariableValue, "sum", "{x} + {y}"),
            new(ActionType.Say, "Результат: {sum}")
        };
        await executor.ExecuteAsync(calcActions, context);
        Assert.Contains("Результат: 20", voice.SpokenPhrases);

        // 2. Loop decrement: c = 3; c = {c} - 1
        context.SetVariable("c", "3");
        var decrementAction = new List<CommandAction>
        {
            new(ActionType.SetVariableValue, "c", "{c} - 1")
        };
        await executor.ExecuteAsync(decrementAction, context);
        Assert.Equal("2", context.GetVariable("c"));
    }

    [Fact]
    public void CommandMatcher_ExactAndSlots_MatchesCorrectly()
    {
        var matcher = new CommandMatcher();

        var pack = new CommandPack { Name = "General", IsActive = true };
        var group = new CommandGroup { Name = "Apps", IsEnabled = true };

        var openAppCmd = new VoiceCommand
        {
            Name = "Open App",
            IsEnabled = true,
            Phrases = ["открой {app}", "запусти {app}"]
        };

        var calcCmd = new VoiceCommand
        {
            Name = "Calculator",
            IsEnabled = true,
            Phrases = ["калькулятор"]
        };

        group.Commands.Add(openAppCmd);
        group.Commands.Add(calcCmd);
        pack.Groups.Add(group);

        var packs = new List<CommandPack> { pack };

        var match1 = matcher.FindMatch("калькулятор", packs, "");
        Assert.NotNull(match1);
        Assert.Equal("Calculator", match1.Command.Name);

        var match2 = matcher.FindMatch("открой блокнот", packs, "");
        Assert.NotNull(match2);
        Assert.Equal("Open App", match2.Command.Name);
        Assert.True(match2.ExtractedVariables.ContainsKey("app"));
        Assert.Equal("блокнот", match2.ExtractedVariables["app"]);
    }

    [Fact]
    public void LaitisImporter_ImportRealBackup_LoadsPacksAndCommands()
    {
        var backupPath = @"C:\Users\mjkey\AppData\Local\Laitis\SaveBackups\Save_2026-09-16_02-09-30.laitis";
        if (!File.Exists(backupPath))
            return;

        var packs = LaitisImporter.ImportFromFile(backupPath, "laitis");

        Assert.NotEmpty(packs);
        Assert.True(packs.Count >= 5);

        Assert.Contains(packs, p => p.Name.Contains("Windows"));
        Assert.Contains(packs, p => p.Name.Contains("YouTube"));

        var winPack = packs.First(p => p.Name.Contains("Windows"));
        var allCommands = winPack.Groups.SelectMany(g => g.Commands).ToList();
        Assert.True(allCommands.Count > 20);

        Assert.Contains(allCommands, c => c.Phrases.Any(phrase => phrase.Contains("блокнот")));
    }

    [Fact]
    public async Task EndToEnd_ExecuteCalculatorCommand_FromRealBackup()
    {
        var backupPath = @"C:\Users\mjkey\AppData\Local\Laitis\SaveBackups\Save_2026-09-16_02-09-30.laitis";
        if (!File.Exists(backupPath))
            return;

        var packs = LaitisImporter.ImportFromFile(backupPath, "laitis");
        var matcher = new CommandMatcher();
        var executor = new ActionExecutor();

        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();

        // Тестируем фразу: "Сколько будет 25 + 15"
        var match = matcher.FindMatch("сколько будет 25 + 15", packs, "");
        Assert.NotNull(match);
        Assert.True(match.ExtractedVariables.ContainsKey("x"), $"Matched cmd: {match.Command.Name}, Phrases: {string.Join(";", match.Command.Phrases)}, Vars: {string.Join(";", match.ExtractedVariables.Keys)}, Conf: {match.Confidence}");

        Assert.Equal("25", match.ExtractedVariables["x"]);
        Assert.Equal("15", match.ExtractedVariables["y"]);

        var context = new MacroExecutionContext(input, windows, voice);
        foreach (var (k, v) in match.ExtractedVariables)
            context.Variables[k] = v;

        await executor.ExecuteAsync(match.Command.Actions, context);

        Assert.True(context.Variables.ContainsKey("z"));
        Assert.Equal("40", context.Variables["z"]);
        Assert.Contains("40", voice.SpokenPhrases);
    }

    [Fact]
    public void VoskSpeechRecognizer_LoadRealModel_Succeeds()
    {
        var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReLaitis", "Models", "vosk-model-small-ru-0.22");
        if (!Directory.Exists(modelPath))
            return;

        using var recognizer = new ReLaitis.Audio.Recognition.VoskSpeechRecognizer();
        var loaded = recognizer.LoadModel(modelPath);

        Assert.True(loaded);
        Assert.True(recognizer.IsModelLoaded);
    }

    [Fact]
    public void PackStorage_SaveAndLoad_RoundTripPreservesAllData()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ReLaitis_Test_{Guid.NewGuid():N}.json");
        try
        {
            var pack = new CommandPack
            {
                Name = "Тестовый пакет",
                ProcessFilter = "chrome.exe",
                IsActive = true
            };
            var group = new CommandGroup { Name = "Группа 1" };
            var command = new VoiceCommand
            {
                Name = "Тестовая команда",
                Phrases = ["тест раз", "тест два"],
                Actions =
                [
                    new CommandAction(ActionType.Hotkeys, "Ctrl+Shift+Esc"),
                    new CommandAction(ActionType.Say, "Привет из теста"),
                    new CommandAction(ActionType.SetVariableValue, "val", "123")
                ]
            };
            group.Commands.Add(command);
            pack.Groups.Add(group);

            PackStorage.SavePacks([pack], tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = PackStorage.LoadPacks(tempFile);
            Assert.Single(loaded);
            Assert.Equal("Тестовый пакет", loaded[0].Name);
            Assert.Equal("chrome.exe", loaded[0].ProcessFilter);
            Assert.Single(loaded[0].Groups);
            Assert.Single(loaded[0].Groups[0].Commands);
            Assert.Equal(3, loaded[0].Groups[0].Commands[0].Actions.Count);
            Assert.Equal("Ctrl+Shift+Esc", loaded[0].Groups[0].Commands[0].Actions[0].Parameters[0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void CommandAction_DisplayDescription_FormatsCorrectly()
    {
        var hotkeyAction = new CommandAction(ActionType.Hotkeys, "Win+D");
        Assert.Contains("Win+D", hotkeyAction.DisplayDescription);

        var sayAction = new CommandAction(ActionType.Say, "Привет мир");
        Assert.Contains("Привет мир", sayAction.DisplayDescription);

        var pauseAction = new CommandAction(ActionType.Pause, "500");
        Assert.Contains("500", pauseAction.DisplayDescription);

        var setVarAction = new CommandAction(ActionType.SetVariableValue, "res", "{x} + {y}");
        Assert.Contains("{res}", setVarAction.DisplayDescription);
        Assert.Contains("{x} + {y}", setVarAction.DisplayDescription);
    }

    [Fact]
    public void VoiceCommand_PrimaryPhrase_HandlesEmptyAndNonEmpty()
    {
        var cmdWithPhrases = new VoiceCommand { Name = "Cmd1", Phrases = ["фраза 1", "фраза 2"] };
        Assert.Equal("фраза 1", cmdWithPhrases.PrimaryPhrase);

        var cmdWithoutPhrases = new VoiceCommand { Name = "Cmd2", Phrases = [] };
        Assert.Equal("(нет фраз)", cmdWithoutPhrases.PrimaryPhrase);
    }

    [Fact]
    public void CommandAction_NewActions_DisplayDescription_FormatsCorrectly()
    {
        var urlAction = new CommandAction(ActionType.OpenURL, "https://google.com");
        Assert.Contains("https://google.com", urlAction.DisplayDescription);

        var httpAction = new CommandAction(ActionType.HttpWebRequest, "https://api.example.com", "POST");
        Assert.Contains("HTTP POST", httpAction.DisplayDescription);

        var toggleAction = new CommandAction(ActionType.TogglePackActivity, "Игры");
        Assert.Contains("Игры", toggleAction.DisplayDescription);

        var timerAction = new CommandAction(ActionType.ScheduleEvent, "5000", "выключи свет");
        Assert.Contains("5000", timerAction.DisplayDescription);
        Assert.Contains("выключи свет", timerAction.DisplayDescription);

        var waitAction = new CommandAction(ActionType.WaitNextCommand, "response");
        Assert.Contains("response", waitAction.DisplayDescription);
    }

    [Fact]
    public async Task ActionExecutor_TogglePackAndSchedule_TriggersCallbacks()
    {
        var executor = new ActionExecutor();
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);

        string? toggledPack = null;
        int toggledState = -1;
        context.TogglePackRequested += (p, s) =>
        {
            toggledPack = p;
            toggledState = s;
        };

        int scheduledDelay = -1;
        string? scheduledPhrase = null;
        context.ScheduleEventRequested += (d, p) =>
        {
            scheduledDelay = d;
            scheduledPhrase = p;
        };

        var actions = new List<CommandAction>
        {
            new(ActionType.TogglePackActivity, "Браузер", "1"),
            new(ActionType.ScheduleEvent, "3000", "закрыть вкладку")
        };

        await executor.ExecuteAsync(actions, context);

        Assert.Equal("Браузер", toggledPack);
        Assert.Equal(1, toggledState);
        Assert.Equal(3000, scheduledDelay);
        Assert.Equal("закрыть вкладку", scheduledPhrase);
    }

    [Fact]
    public async Task ActionExecutor_WaitNextCommand_SavesPhraseToVariable()
    {
        var executor = new ActionExecutor();
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);

        context.WaitForNextPhraseAsync = (timeout) => Task.FromResult<string?>("да подтверждаю");

        var actions = new List<CommandAction>
        {
            new(ActionType.WaitNextCommand, "userChoice", "3000")
        };

        await executor.ExecuteAsync(actions, context);

        Assert.True(context.Variables.ContainsKey("userChoice"));
        Assert.Equal("да подтверждаю", context.Variables["userChoice"]);
    }

    [Fact]
    public void JetAimCalculator_CalculateCoordinates_MathIsAccurate()
    {
        double width = 1920;
        double height = 1080;

        // Центр экрана (сектор 5)
        var (cx, cy) = JetAimCalculator.CalculateSectorCenter(5, width, height);
        Assert.Equal(960, cx);
        Assert.Equal(540, cy);

        // Левый верхний сектор (сектор 1)
        var (x1, y1) = JetAimCalculator.CalculateSectorCenter(1, width, height);
        Assert.Equal(320, x1);
        Assert.Equal(180, y1);

        // Правый нижний сектор (сектор 9)
        var (x9, y9) = JetAimCalculator.CalculateSectorCenter(9, width, height);
        Assert.Equal(1600, x9);
        Assert.Equal(900, y9);

        // Двухуровневый субсектор: сектор 5, подсектор 5 -> все равно центр
        var (subCx, subCy) = JetAimCalculator.CalculateSubSectorCenter(5, 5, width, height);
        Assert.Equal(960, subCx);
        Assert.Equal(540, subCy);
    }

    [Fact]
    public void JetAimCalculator_ParseSectorNumber_ParsesDigitsAndRussian()
    {
        Assert.Equal(1, JetAimCalculator.ParseSectorNumber("1"));
        Assert.Equal(1, JetAimCalculator.ParseSectorNumber("один"));
        Assert.Equal(1, JetAimCalculator.ParseSectorNumber("первый"));
        Assert.Equal(5, JetAimCalculator.ParseSectorNumber("пять"));
        Assert.Equal(9, JetAimCalculator.ParseSectorNumber("9"));
        Assert.Equal(9, JetAimCalculator.ParseSectorNumber("девять"));
        Assert.Null(JetAimCalculator.ParseSectorNumber("привет"));
    }

    [Fact]
    public void DictationFormatter_PunctuationAndCapitalization_WorksAccurately()
    {
        // Проверка знаков препинания и капитализации первого слова
        var res1 = DictationFormatter.FormatSpokenText("привет мир запятая как дела точка");
        Assert.Equal("Привет мир, как дела.", res1);

        // Проверка составных знаков («восклицательный знак»)
        var res2 = DictationFormatter.FormatSpokenText("отличная работа восклицательный знак");
        Assert.Equal("Отличная работа!", res2);

        // Проверка составного знака «с новой строки»
        var res3 = DictationFormatter.FormatSpokenText("первая строка новая строка вторая строка точка");
        Assert.Equal("Первая строка\nВторая строка.", res3);
    }

    [Fact]
    public void SettingsStorage_SaveAndLoad_WorksCorrectly()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"Settings_Test_{Guid.NewGuid():N}.json");
        try
        {
            var settings = new UserSettings
            {
                SelectedAudioDevice = 2,
                ListeningMode = ListeningMode.WakeWord,
                WakeWord = "компьютер",
                PushToTalkKey = "ScrollLock",
                SoundFeedbackEnabled = false,
                HudOpacity = 0.85
            };

            SettingsStorage.SaveSettings(settings, tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = SettingsStorage.LoadSettings(tempFile);
            Assert.Equal(2, loaded.SelectedAudioDevice);
            Assert.Equal(ListeningMode.WakeWord, loaded.ListeningMode);
            Assert.Equal("компьютер", loaded.WakeWord);
            Assert.Equal("ScrollLock", loaded.PushToTalkKey);
            Assert.False(loaded.SoundFeedbackEnabled);
            Assert.Equal(0.85, loaded.HudOpacity);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void PackStorage_SinglePack_SaveAndLoad_WorksCorrectly()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"Pack_Test_{Guid.NewGuid():N}.json");
        try
        {
            var pack = new CommandPack
            {
                Name = "Тестовый Экспорт Пакет",
                ProcessFilter = "notepad",
                IsActive = true,
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Тестовая группа",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Тестовая команда",
                                Phrases = ["проверка экспорта"],
                                Actions = [new CommandAction(ActionType.Say, "Тест успешен")]
                            }
                        ]
                    }
                ]
            };

            PackStorage.SaveSinglePack(pack, tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = PackStorage.LoadSinglePack(tempFile);
            Assert.NotNull(loaded);
            Assert.Equal("Тестовый Экспорт Пакет", loaded.Name);
            Assert.Equal("notepad", loaded.ProcessFilter);
            Assert.Single(loaded.Groups);
            Assert.Single(loaded.Groups[0].Commands);
            Assert.Equal("Тестовая команда", loaded.Groups[0].Commands[0].Name);
            Assert.Equal("проверка экспорта", loaded.Groups[0].Commands[0].Phrases[0]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void PackStorage_HumanReadableFormat_ContainsCleanKeysAndActionNames()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"Readable_Test_{Guid.NewGuid():N}.json");
        try
        {
            var pack = new CommandPack
            {
                Name = "Читабельный Пакет",
                ProcessFilter = "code.exe",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Редактор",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Сохранить файл",
                                Phrases = ["сохрани файл", "сохранить"],
                                Actions =
                                [
                                    new CommandAction(ActionType.Hotkeys, "Ctrl+S"),
                                    new CommandAction(ActionType.Say, "Файл сохранен")
                                ]
                            }
                        ]
                    }
                ]
            };

            PackStorage.SavePacks([pack], tempFile);
            var json = File.ReadAllText(tempFile);

            // Проверяем наличие читабельных понятных свойств
            Assert.Contains("\"name\": \"Читабельный Пакет\"", json);
            Assert.Contains("\"processFilter\": \"code.exe\"", json);
            Assert.Contains("\"groups\":", json);
            Assert.Contains("\"commands\":", json);
            Assert.Contains("\"phrases\":", json);
            Assert.Contains("\"actions\":", json);
            Assert.Contains("\"type\": \"Hotkeys\"", json);
            Assert.Contains("\"type\": \"Say\"", json);
            Assert.Contains("\"parameters\":", json);

            // Проверяем отсутствие старых криптических ключей Laitis
            Assert.DoesNotContain("\"T\":", json);
            Assert.DoesNotContain("\"P\":", json);
            Assert.DoesNotContain("\"N\":", json);
            Assert.DoesNotContain("\"V\":", json);
            Assert.DoesNotContain("\"C\":", json);
            Assert.DoesNotContain("\"E\":", json);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void PackStorage_LlmGeneratedJson_ParsesDirectCommandsAndLowercaseActions()
    {
        // Пример ультра-лаконичного JSON, составленного нейросетью или человеком вручную:
        // без GUID, без groups (commands прямо в корне), тип действия в lowercase,
        // параметры строкой вместо массива, triggers вместо phrases.
        var llmJson = """
        [
          {
            "name": "Нейропак для Дискорда",
            "processFilter": "discord.exe",
            "commands": [
              {
                "triggers": ["выключи микрофон", "заглуши меня"],
                "actions": [
                  {
                    "type": "hotkeys",
                    "parameters": "Ctrl+Shift+M"
                  },
                  {
                    "type": "say",
                    "parameters": "Микрофон отключен"
                  }
                ]
              }
            ]
          }
        ]
        """;

        var packs = PackStorage.ParsePacksJson(llmJson);
        Assert.Single(packs);
        var pack = packs[0];
        Assert.Equal("Нейропак для Дискорда", pack.Name);
        Assert.Equal("discord.exe", pack.ProcessFilter);
        Assert.Single(pack.Groups); // Автоматически обернуто в группу "Общие"
        Assert.Single(pack.Groups[0].Commands);

        var cmd = pack.Groups[0].Commands[0];
        Assert.Equal("выключи микрофон", cmd.Name); // Имя авто-подставлено из первой фразы
        Assert.Equal(2, cmd.Phrases.Count);
        Assert.Equal("выключи микрофон", cmd.Phrases[0]);
        Assert.Equal("заглуши меня", cmd.Phrases[1]);
        Assert.Equal(2, cmd.Actions.Count);

        Assert.Equal(ActionType.Hotkeys, cmd.Actions[0].Type);
        Assert.Equal("Ctrl+Shift+M", cmd.Actions[0].Parameters[0]);

        Assert.Equal(ActionType.Say, cmd.Actions[1].Type);
        Assert.Equal("Микрофон отключен", cmd.Actions[1].Parameters[0]);
    }

    [Fact]
    public void PackStorage_LegacyLaitisJson_AutoMigratesAndLoadsCorrectly()
    {
        // Старый формат Laitis из Save.json
        var legacyJson = """
        [
          {
            "ID": "pack1",
            "N": "Старый Laitis Пак",
            "C": "ru-RU",
            "A": true,
            "P": [
              {
                "ID": "grp1",
                "N": "Старая группа",
                "E": true,
                "C": [
                  {
                    "ID": "cmd1",
                    "N": "Калькулятор",
                    "E": true,
                    "V": ["калькулятор"],
                    "A": [
                      {
                        "T": 0,
                        "P": ["calc.exe"]
                      }
                    ]
                  }
                ]
              }
            ]
          }
        ]
        """;

        var tempFile = Path.Combine(Path.GetTempPath(), $"Legacy_Test_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, legacyJson);
            var packs = PackStorage.LoadPacks(tempFile);

            Assert.Single(packs);
            Assert.Equal("Старый Laitis Пак", packs[0].Name);
            Assert.Single(packs[0].Groups);
            Assert.Single(packs[0].Groups[0].Commands);
            Assert.Equal("калькулятор", packs[0].Groups[0].Commands[0].Phrases[0]);
            Assert.Equal(ActionType.OpenFile, packs[0].Groups[0].Commands[0].Actions[0].Type);
            Assert.Equal("calc.exe", packs[0].Groups[0].Commands[0].Actions[0].Parameters[0]);

            // Проверяем, что файл на диске автоматически пересохранился в чистый читабельный формат
            var migratedJson = File.ReadAllText(tempFile);
            Assert.Contains("\"name\": \"Старый Laitis Пак\"", migratedJson);
            Assert.Contains("\"type\": \"OpenFile\"", migratedJson);
            Assert.DoesNotContain("\"T\":", migratedJson);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void PackStorage_MigrateUserPacksIfPresent()
    {
        if (PackStorage.HasSavedPacks())
        {
            var packs = PackStorage.LoadPacks();
            Assert.NotEmpty(packs);
        }
    }

    [Fact]
    public void PackStorage_DefaultShowcasePacks_ContainAllNonBrowserFeaturesAndSaveLoadCleanly()
    {
        var packs = PackStorage.GetDefaultShowcasePacks();
        Assert.Equal(4, packs.Count);

        // Проверяем имена пакетов
        Assert.Contains(packs, p => p.Name.Contains("Система и Управление"));
        Assert.Contains(packs, p => p.Name.Contains("Навигация, Мышь и JetAim"));
        Assert.Contains(packs, p => p.Name.Contains("Умный Ассистент и Логика"));
        Assert.Contains(packs, p => p.Name.Contains("Профиль: Блокнот") && p.ProcessFilter == "notepad.exe");

        // Собираем все типы действий в пакетах
        var allActionTypes = packs
            .SelectMany(p => p.Groups)
            .SelectMany(g => g.Commands)
            .SelectMany(c => c.Actions)
            .Select(a => a.Type)
            .ToHashSet();

        // Проверяем наличие ключевых действий
        Assert.Contains(ActionType.OpenFile, allActionTypes);
        Assert.Contains(ActionType.CloseApp, allActionTypes);
        Assert.Contains(ActionType.Hotkeys, allActionTypes);
        Assert.Contains(ActionType.MouseMove, allActionTypes);
        Assert.Contains(ActionType.Say, allActionTypes);
        Assert.Contains(ActionType.TypeText, allActionTypes);
        Assert.Contains(ActionType.PlayAudio, allActionTypes);
        Assert.Contains(ActionType.OpenURL, allActionTypes);
        Assert.Contains(ActionType.Pause, allActionTypes);
        Assert.Contains(ActionType.IfProcessExists, allActionTypes);
        Assert.Contains(ActionType.MouseButton, allActionTypes);
        Assert.Contains(ActionType.HttpWebRequest, allActionTypes);
        Assert.Contains(ActionType.SetVariableValue, allActionTypes);
        Assert.Contains(ActionType.IfVariableValue, allActionTypes);
        Assert.Contains(ActionType.ShowWindow, allActionTypes);
        Assert.Contains(ActionType.MouseMoveOn, allActionTypes);
        Assert.Contains(ActionType.MouseScroll, allActionTypes);
        Assert.Contains(ActionType.TogglePackActivity, allActionTypes);
        Assert.Contains(ActionType.RandomActionBlock, allActionTypes);
        Assert.Contains(ActionType.Loop, allActionTypes);
        Assert.Contains(ActionType.JetAim, allActionTypes);
        Assert.Contains(ActionType.BatchScript, allActionTypes);
        Assert.Contains(ActionType.WaitNextPhrase, allActionTypes);
        Assert.Contains(ActionType.Notify, allActionTypes);

        // Убеждаемся, что в демонстрационном наборе НЕТ действий браузерного расширения
        Assert.DoesNotContain(ActionType.WebPageClick, allActionTypes);
        Assert.DoesNotContain(ActionType.WebPageScript, allActionTypes);
        Assert.DoesNotContain(ActionType.WebPagePopupOpen, allActionTypes);

        // Проверяем полный цикл сериализации и десериализации
        var tempFile = Path.Combine(Path.GetTempPath(), $"Showcase_Test_{Guid.NewGuid():N}.json");
        try
        {
            PackStorage.SavePacks(packs, tempFile);
            var loaded = PackStorage.LoadPacks(tempFile);
            Assert.Equal(4, loaded.Count);
            Assert.Equal(packs.Sum(p => p.TotalCommandsCount), loaded.Sum(p => p.TotalCommandsCount));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ActionExecutor_BrowserActions_WorkCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var browser = new MockBrowserBridge();
        var executor = new ActionExecutor();

        var context = new MacroExecutionContext(input, windows, voice, browserBridge: browser);

        var actions = new List<CommandAction>
        {
            new(ActionType.WebPageClick, "14"),
            new(ActionType.WebPageNavigate, "https://github.com/trending"),
            new(ActionType.WebPageScript, "hints"),
            new(ActionType.WebPageScript, "hide_hints"),
            new(ActionType.WebPageScript, "console.log('test')"),
            new(ActionType.WebPageFocus, "#search-box"),
            new(ActionType.WebPageGetText, "#title", "PageTitle"),
            new(ActionType.WebPagePopupOpen, "new")
        };

        await executor.ExecuteAsync(actions, context);

        Assert.Contains("14", browser.ClickedTargets);
        Assert.Contains("https://github.com/trending", browser.NavigatedUrls);
        Assert.Contains(true, browser.HintsToggled);
        Assert.Contains(false, browser.HintsToggled);
        Assert.Contains("console.log('test')", browser.ExecutedScripts);
        Assert.Contains("document.querySelector('#search-box')?.focus();", browser.ExecutedScripts);
        Assert.Equal("Mock Element Text", context.Variables["PageTitle"]);
        Assert.Contains("new", browser.TabCommands);
    }

    [Fact]
    public async Task ActionExecutor_IfWebsiteSelected_WorksCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var browser = new MockBrowserBridge { CurrentUrl = "https://youtube.com/watch", CurrentTitle = "Cool Video - YouTube" };
        var executor = new ActionExecutor();

        var context = new MacroExecutionContext(input, windows, voice, browserBridge: browser);

        var actions = new List<CommandAction>
        {
            new(ActionType.IfWebsiteSelected, "youtube"),
                new(ActionType.SetVariableValue, "matched", "yes"),
            new(ActionType.EndBlock)
        };

        await executor.ExecuteAsync(actions, context);
        Assert.Equal("yes", context.Variables["matched"]);

        var actionsNotMatch = new List<CommandAction>
        {
            new(ActionType.IfWebsiteSelected, "twitch"),
                new(ActionType.SetVariableValue, "matched2", "yes"),
            new(ActionType.Else),
                new(ActionType.SetVariableValue, "matched2", "no"),
            new(ActionType.EndBlock)
        };

        await executor.ExecuteAsync(actionsNotMatch, context);
        Assert.Equal("no", context.Variables["matched2"]);
    }

    [Fact]
    public void MacroExecutionContext_BrowserVariables_ResolveCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var browser = new MockBrowserBridge { CurrentUrl = "https://example.org", CurrentTitle = "Welcome" };

        var context = new MacroExecutionContext(input, windows, voice, browserBridge: browser);

        var resolved = context.ResolveVariables("URL: {BrowserUrl}, Заголовок: {BrowserTitle}");
        Assert.Equal("URL: https://example.org, Заголовок: Welcome", resolved);
    }

    [Fact]
    public async Task BrowserBridgeServer_HttpEndpoints_WorkCorrectly()
    {
        var port = 11388;
        using var server = new BrowserBridgeServer(port);
        server.Start();

        try
        {
            using var client = new HttpClient();
            var resp = await client.GetStringAsync($"http://127.0.0.1:{port}/status");
            Assert.Contains("\"status\":\"ok\"", resp);
            Assert.Contains("ReLaitis Voice Bridge", resp);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void WhisperSettings_SaveAndLoad_WorksCorrectly()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"WhisperSettings_Test_{Guid.NewGuid():N}.json");
        try
        {
            var settings = new UserSettings
            {
                SpeechEngine = SpeechEngineType.Whisper,
                WhisperModel = "tiny"
            };

            SettingsStorage.SaveSettings(settings, tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = SettingsStorage.LoadSettings(tempFile);
            Assert.Equal(SpeechEngineType.Whisper, loaded.SpeechEngine);
            Assert.Equal("tiny", loaded.WhisperModel);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void CommandMatcher_CommandPreconditions_SkipsWhenProcessNotActive()
    {
        var packs = new List<CommandPack>
        {
            new()
            {
                Name = "Калькулятор",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Математика",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Вычислить",
                                Phrases = ["{выражение}"],
                                Actions =
                                [
                                    new CommandAction(ActionType.IfProcessSelected, "Calculator.exe/Calc.exe"),
                                    new CommandAction(ActionType.TypeText, "result")
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var matcher = new CommandMatcher();

        // When Calculator is NOT active:
        var matchInactive = matcher.FindMatch("два плюс два", packs, "", isProcessActive: filter => false);
        Assert.Null(matchInactive);

        // When Calculator IS active:
        var matchActive = matcher.FindMatch("два плюс два", packs, "", isProcessActive: filter => true);
        Assert.NotNull(matchActive);
        Assert.Equal("Вычислить", matchActive.Command.Name);
        Assert.Equal("два плюс два", matchActive.ExtractedVariables["выражение"]);
    }

    [Fact]
    public void LaitisCompatibility_ActionDescriptions_MatchLaitisFormat()
    {
        var mouseMoveOn = new CommandAction(ActionType.MouseMoveOn, "", "{название}");
        Assert.Equal("Навести курсор на элемент {название}", mouseMoveOn.DisplayDescription);

        var rightClick = new CommandAction(ActionType.MouseButton, "2", "0");
        Assert.Equal("Нажать Правая кнопка мыши", rightClick.DisplayDescription);

        var leftClick = new CommandAction(ActionType.MouseButton, "0", "0");
        Assert.Equal("Нажать Левая кнопка мыши", leftClick.DisplayDescription);

        var setVarReplace = new CommandAction(ActionType.SetVariableValue, "выражение", "плюс", "13", "+");
        Assert.Equal("Задать переменной {выражение} значение плюс Заменить на +", setVarReplace.DisplayDescription);

        var ifProcess = new CommandAction(ActionType.IfProcessSelected, "Calculator.exe/Calc.exe");
        Assert.Equal("Если активна программа Calculator.exe/Calc.exe", ifProcess.DisplayDescription);

        var typeText = new CommandAction(ActionType.TypeText, "{выражение}");
        Assert.Equal("Напечатать текст {выражение}", typeText.DisplayDescription);
    }

    [Fact]
    public async Task ActionExecutor_SetVariable_ReplaceOperation_UpdatesContextCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var executor = new ActionExecutor();
        var context = new MacroExecutionContext(input, windows, voice);
        context.SetVariable("выражение", "два плюс три");

        var replaceAction = new CommandAction(ActionType.SetVariableValue, "выражение", "плюс", "13", "+");
        await executor.ExecuteAsync([replaceAction], context);

        Assert.Equal("два + три", context.GetVariable("выражение"));
    }

    [Fact]
    public void VoiceCommand_DisplayTitleAndSubtitle_FormatCorrectly()
    {
        var cmd = new VoiceCommand
        {
            Name = "кликнуть на {название}",
            Phrases = ["кликнуть на {название}", "клинуть {название}", "нажать {название}"],
            Actions =
            [
                new CommandAction(ActionType.MouseMoveOn, "", "{название}"),
                new CommandAction(ActionType.MouseButton, "0", "0")
            ]
        };

        Assert.Equal("кликнуть на {название}, клинуть {название}, нажать {название}", cmd.DisplayTitle);
        Assert.Equal("Навести курсор на элемент {название} • Нажать Левая кнопка мыши", cmd.DisplaySubtitle);
    }

    [Fact]
    public void WindowsWindowManager_FindAndClickElementByName_HandlesSafely()
    {
        var wm = new WindowsWindowManager();
        Assert.False(wm.FindAndClickElementByName(""));
        Assert.False(wm.FindAndClickElementByName("   "));
        Assert.False(wm.FindAndClickElementByName("{несуществующий_элемент_123456789}"));
    }

    [Fact]
    public void CommandMatcher_PureWildcard_MathTokenValidation()
    {
        var packs = new List<CommandPack>
        {
            new()
            {
                Name = "Калькулятор",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Математика",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Считать",
                                Phrases = ["{выражение}"],
                                Actions = [new CommandAction(ActionType.Say, "ok")]
                            }
                        ]
                    }
                ]
            }
        };

        var matcher = new CommandMatcher();

        // Non-math phrase should NOT match {выражение}
        var matchWords = matcher.FindMatch("привет как дела", packs, "");
        Assert.Null(matchWords);

        // Math phrase with Russian words should match
        var matchMath = matcher.FindMatch("пять умножить на шесть", packs, "");
        Assert.NotNull(matchMath);
        Assert.Equal("Считать", matchMath.Command.Name);
    }

    [Fact]
    public void CommandMatcher_PrioritizesExactOrLiteralOverPureWildcard()
    {
        var packs = new List<CommandPack>
        {
            new()
            {
                Name = "Общий",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Команды",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Адаптивная команда",
                                Phrases = ["{название}"],
                                Actions = [new CommandAction(ActionType.Say, "adaptive")]
                            },
                            new VoiceCommand
                            {
                                Name = "Открыть блокнот",
                                Phrases = ["открыть блокнот"],
                                Actions = [new CommandAction(ActionType.OpenFile, "notepad.exe")]
                            }
                        ]
                    }
                ]
            }
        };

        var matcher = new CommandMatcher();
        var match = matcher.FindMatch("открыть блокнот", packs, "");
        Assert.NotNull(match);
        // Must match "Открыть блокнот", NOT the wildcard "{название}"
        Assert.Equal("Открыть блокнот", match.Command.Name);
    }

    [Fact]
    public void GlobalVariablesStorage_SaveAndLoad_WorksCorrectly()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"GlobalVars_Test_{Guid.NewGuid():N}.json");
        try
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bot_name"] = "ReLaitis AI",
                ["volume_step"] = "5"
            };

            GlobalVariablesStorage.SaveVariables(dict, tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = GlobalVariablesStorage.LoadVariables(tempFile);
            Assert.Equal(2, loaded.Count);
            Assert.Equal("ReLaitis AI", loaded["bot_name"]);
            Assert.Equal("5", loaded["volume_step"]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteWhile_LoopsUntilConditionFails()
    {
        var executor = new ActionExecutor();
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);

        context.SetVariable("counter", "0");

        var actions = new List<CommandAction>
        {
            // While counter < 3
            new CommandAction(ActionType.While, "counter", "3", "3"),
            // counter = counter + 1
            new CommandAction(ActionType.SetVariableValue, "counter", "{counter}", "1", "1"),
            new CommandAction(ActionType.EndBlock)
        };

        await executor.ExecuteAsync(actions, context);

        Assert.Equal("3", context.Variables["counter"]);
    }

    [Fact]
    public async Task ExecuteScheduleEvent_CanonicalParameterLayout()
    {
        var executor = new ActionExecutor();
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice);

        int receivedDelay = -1;
        string? receivedPhrase = null;
        context.ScheduleEventRequested += (delay, phrase) =>
        {
            receivedDelay = delay;
            receivedPhrase = phrase;
        };

        // Canonical 3-param: P[0]=Type (0: Delay), P[1]=Time/Delay (1500), P[2]=Phrase
        var action = new CommandAction(ActionType.ScheduleEvent, "0", "1500", "запусти таймер");
        await executor.ExecuteAsync([action], context);

        Assert.Equal(1500, receivedDelay);
        Assert.Equal("запусти таймер", receivedPhrase);
    }

    [Fact]
    public async Task ExecuteMouseMove_PercentTranslationCalculatesCorrectly()
    {
        var executor = new ActionExecutor();
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        var context = new MacroExecutionContext(input, windows, voice)
        {
            ScreenWidth = 1920,
            ScreenHeight = 1080
        };

        // P[0]="50", P[1]="50", P[2]="0" (Instant), P[3]="1" (Percent)
        var action = new CommandAction(ActionType.MouseMove, "50", "50", "0", "1");
        await executor.ExecuteAsync([action], context);

        Assert.Single(input.MouseMoves);
        Assert.Equal(960, input.MouseMoves[0].X);
        Assert.Equal(540, input.MouseMoves[0].Y);
    }

    [Fact]
    public void EvaluateCondition_SlashDelimitedProcesses_MatchesAny()
    {
        Assert.True(CommandMatcher.IsProcessMatchingFilter("dota2/dota2.exe/steam", "dota2.exe"));
        Assert.True(CommandMatcher.IsProcessMatchingFilter("notepad/calc", "calc.exe"));
        Assert.True(CommandMatcher.IsProcessMatchingFilter("notepad/calc", "calc"));
        Assert.False(CommandMatcher.IsProcessMatchingFilter("notepad/calc", "chrome.exe"));
    }

    [Fact]
    public void JetAimCalculator_Recursive5Levels_CalculatesExactCoordinates()
    {
        // Level 1: Sector 5 -> center (960, 540) on 1920x1080
        // Level 5: Sector 5 inside Sector 5... -> center is still (960, 540)
        var center = JetAimCalculator.CalculateRecursiveCenter([5, 5, 5, 5, 5], 1920, 1080);
        Assert.Equal(960, center.X);
        Assert.Equal(540, center.Y);

        var bounds = JetAimCalculator.CalculateRecursiveBounds([1, 9], 1920, 1080);
        Assert.True(bounds.Left >= 426 && bounds.Left <= 427);
        Assert.True(bounds.Top >= 240 && bounds.Top <= 241);
    }

    [Fact]
    public void JetAimCalculator_SectorSequence_ParsesCorrectly()
    {
        var s1 = JetAimCalculator.ParseSectorSequence("528");
        Assert.Equal([5, 2, 8], s1);

        var s2 = JetAimCalculator.ParseSectorSequence("пять два восемь");
        Assert.Equal([5, 2, 8], s2);

        var s3 = JetAimCalculator.ParseSectorSequence("двадцать пять");
        Assert.Equal([2, 5], s3);

        var s4 = JetAimCalculator.ParseSectorSequence("семьсот тридцать два");
        Assert.Equal([7, 3, 2], s4);
    }

    [Fact]
    public void ApplyRules_ReplacesWordsCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice);

        var pack = new CommandPack
        {
            Name = "Математика",
            IsActive = true,
            Rules =
            [
                new Rule { Word = "плюс", Replacement = "+" },
                new Rule { Word = "минус", Replacement = "-" },
                new Rule { Word = "умножить на", Replacement = "*" }
            ]
        };
        coordinator.Packs.Add(pack);

        var modified = coordinator.ApplyPackRules("сколько будет двадцать плюс пятнадцать");
        Assert.Equal("сколько будет двадцать + пятнадцать", modified);
    }

    [Fact]
    public void VoiceAudioCache_SavesAndRetrievesAudio()
    {
        var sampleText = "тест кэша";
        var sampleData = new byte[128];
        Array.Fill<byte>(sampleData, 0x42);

        VoiceAudioCache.SaveAudio(sampleText, "GoogleTest", sampleData);

        var retrieved = VoiceAudioCache.GetCachedAudio(sampleText, "GoogleTest");
        Assert.NotNull(retrieved);
        Assert.Equal(sampleData, retrieved);

        // Phrases > 50 characters must NOT be cached
        var longText = new string('x', 51);
        VoiceAudioCache.SaveAudio(longText, "GoogleTest", sampleData);

        var notRetrieved = VoiceAudioCache.GetCachedAudio(longText, "GoogleTest");
        Assert.Null(notRetrieved);
    }

    [Fact]
    public async Task SpeechEngineCoordinator_MultipleWakeWords_TriggersAnyAndInterpretsPrefix()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice)
        {
            IsWakeWordEnabled = true,
            WakeWordPhrase = "лэйтис, джарвис, слушай, эй ты"
        };

        var executed = false;
        var pack = new CommandPack
        {
            Name = "Тестовый",
            IsActive = true,
            Groups =
            [
                new CommandGroup
                {
                    Commands =
                    [
                        new VoiceCommand
                        {
                            Name = "Открой браузер",
                            Phrases = ["открой браузер"],
                            Actions = [new CommandAction(ActionType.Comment, "done")]
                        }
                    ]
                }
            ]
        };
        coordinator.Packs.Add(pack);
        coordinator.CommandExecuted += (s, e) => executed = true;

        // 1. Without wake word when window inactive -> ignored
        var res1 = await coordinator.ProcessPhraseAsync("открой браузер");
        Assert.False(res1);
        Assert.False(executed);

        // 2. Standalone wake word "джарвис" -> activates window
        var res2 = await coordinator.ProcessPhraseAsync("джарвис");
        Assert.True(res2);
        Assert.True(coordinator.IsWakeWordActive);

        // 3. Command inside active window without wake word -> executes!
        var res3 = await coordinator.ProcessPhraseAsync("открой браузер");
        Assert.True(res3);
        Assert.True(executed);
        Assert.False(coordinator.IsWakeWordActive); // resets after command

        // 4. Combined phrase in one breath: "эй ты открой браузер" -> strips wake word and executes!
        executed = false;
        var res4 = await coordinator.ProcessPhraseAsync("эй ты открой браузер");
        Assert.True(res4);
        Assert.True(executed);

        // 5. Meta-command "замолчи" -> handles immediately
        voice.SpokenPhrases.Add("Длинная речь");
        var res5 = await coordinator.ProcessPhraseAsync("замолчи");
        Assert.True(res5);
    }

    [Fact]
    public async Task SpeechEngineCoordinator_TogglePack_HandlesStatesCorrectly()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice);

        var targetPack = new CommandPack { Name = "Игры", IsActive = true };
        coordinator.Packs.Add(targetPack);

        // Command to Disable (state 2)
        var cmdDisable = new VoiceCommand
        {
            Name = "Выключить игры",
            Phrases = ["выключить игры"],
            Actions = [new CommandAction(ActionType.TogglePackActivity, "Игры", "2")]
        };
        // Command to Enable (state 1)
        var cmdEnable = new VoiceCommand
        {
            Name = "Включить игры",
            Phrases = ["включить игры"],
            Actions = [new CommandAction(ActionType.TogglePackActivity, "Игры", "1")]
        };
        // Command to Toggle (state 0)
        var cmdToggle = new VoiceCommand
        {
            Name = "Переключить игры",
            Phrases = ["переключить игры"],
            Actions = [new CommandAction(ActionType.TogglePackActivity, "Игры", "0")]
        };

        var controlPack = new CommandPack
        {
            Name = "Управление",
            IsActive = true,
            Groups = [new CommandGroup { Commands = [cmdDisable, cmdEnable, cmdToggle] }]
        };
        coordinator.Packs.Add(controlPack);

        // 1. Initially Active (true). Execute Disable (state 2) -> becomes false
        await coordinator.ProcessPhraseAsync("выключить игры");
        Assert.False(targetPack.IsActive);

        // 2. Execute Enable (state 1) -> becomes true
        await coordinator.ProcessPhraseAsync("включить игры");
        Assert.True(targetPack.IsActive);

        // 3. Execute Toggle (state 0) -> becomes false
        await coordinator.ProcessPhraseAsync("переключить игры");
        Assert.False(targetPack.IsActive);

        // 4. Execute Toggle again (state 0) -> becomes true
        await coordinator.ProcessPhraseAsync("переключить игры");
        Assert.True(targetPack.IsActive);
    }

    #region CSharpScript Tests

    [Fact]
    public async Task CSharpScript_ExecutesSimpleExpression_SetsTargetVariable()
    {
        var executor = new ActionExecutor();
        var context = new MacroExecutionContext(
            new MockInputSimulator(),
            new MockWindowManager(),
            new MockVoiceFeedback());

        var action = new CommandAction(ActionType.CSharpScript, "10 * 5", "calcResult");

        await executor.ExecuteAsync([action], context);

        Assert.Equal("50", context.GetVariable("calcResult"));
    }

    [Fact]
    public async Task CSharpScript_CanAccessContextAndModifyVariables()
    {
        var executor = new ActionExecutor();
        var initialVars = new Dictionary<string, string> { ["name"] = "ReLaitis" };
        var context = new MacroExecutionContext(
            new MockInputSimulator(),
            new MockWindowManager(),
            new MockVoiceFeedback(),
            initialVariables: initialVars);

        var code = @"
            var name = Get(""name"");
            Set(""greeting"", $""Привет, {name}!"");
        ";
        var action = new CommandAction(ActionType.CSharpScript, code);

        await executor.ExecuteAsync([action], context);

        Assert.Equal("Привет, ReLaitis!", context.GetVariable("greeting"));
    }

    [Fact]
    public async Task CSharpScript_CanInvokeInputAndVoice()
    {
        var input = new MockInputSimulator();
        var voice = new MockVoiceFeedback();
        var executor = new ActionExecutor();
        var context = new MacroExecutionContext(
            input,
            new MockWindowManager(),
            voice);

        var code = @"
            Input.SendHotkey(""ctrl+c"");
            Say(""Команда выполнена"");
        ";
        var action = new CommandAction(ActionType.CSharpScript, code);

        await executor.ExecuteAsync([action], context);

        Assert.Contains(input.SentHotkeys, h => h.Contains("ctrl+c"));
        Assert.Contains(voice.SpokenPhrases, p => p == "Команда выполнена");
    }

    [Fact]
    public async Task CSharpScript_SupportsAsyncAwait()
    {
        var executor = new ActionExecutor();
        var context = new MacroExecutionContext(
            new MockInputSimulator(),
            new MockWindowManager(),
            new MockVoiceFeedback());

        var code = @"
            await Task.Delay(10);
            Set(""status"", ""ok"");
            return 42 * 2;
        ";
        var action = new CommandAction(ActionType.CSharpScript, code, "result");

        await executor.ExecuteAsync([action], context);

        Assert.Equal("ok", context.GetVariable("status"));
        Assert.Equal("84", context.GetVariable("result"));
    }

    [Fact]
    public async Task CSharpScript_CachesCompiledDelegate()
    {
        CSharpScriptEngine.ClearCache();
        var initialCount = CSharpScriptEngine.CachedCount;
        Assert.Equal(0, initialCount);

        var executor = new ActionExecutor();
        var context = new MacroExecutionContext(
            new MockInputSimulator(),
            new MockWindowManager(),
            new MockVoiceFeedback());

        var action = new CommandAction(ActionType.CSharpScript, "1 + 1", "res");

        await executor.ExecuteAsync([action], context);
        Assert.Equal(1, CSharpScriptEngine.CachedCount);

        // Run same script again
        await executor.ExecuteAsync([action], context);
        Assert.Equal(1, CSharpScriptEngine.CachedCount);
        Assert.Equal("2", context.GetVariable("res"));
    }

    [Fact]
    public async Task CSharpScript_HandlesCompilationErrorGracefully()
    {
        var executor = new ActionExecutor();
        var context = new MacroExecutionContext(
            new MockInputSimulator(),
            new MockWindowManager(),
            new MockVoiceFeedback());

        string? notifiedTitle = null;
        string? notifiedMsg = null;
        context.NotificationTriggered += (title, msg) =>
        {
            notifiedTitle = title;
            notifiedMsg = msg;
        };

        // Syntax error in C# script
        var action = new CommandAction(ActionType.CSharpScript, "invalid syntax + ;;;", "result");

        // Should not throw, should handle cleanly
        await executor.ExecuteAsync([action], context);

        Assert.NotNull(notifiedTitle);
        Assert.Contains("C#", notifiedTitle);
        Assert.NotNull(notifiedMsg);
    }

    #endregion

    #region Journal and Speech Coordinator Events Tests

    [Fact]
    public async Task SpeechEngineCoordinator_UnmatchedPhrase_FiresUnmatchedEvent()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice);

        string? unmatchedPhrase = null;
        coordinator.UnmatchedPhraseRecognized += (s, phrase) => unmatchedPhrase = phrase;

        var res = await coordinator.ProcessPhraseAsync("несуществующая голосовая команда 123");
        Assert.False(res);
        Assert.Equal("несуществующая голосовая команда 123", unmatchedPhrase);
    }

    [Fact]
    public async Task SpeechEngineCoordinator_Dictation_FiresDictationEvent()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice);

        string? dictationText = null;
        coordinator.DictationRecognized += (s, text) => dictationText = text;

        coordinator.SetDictationMode(true);
        var res = await coordinator.ProcessPhraseAsync("привет мир точка");
        Assert.True(res);
        Assert.NotNull(dictationText);
        Assert.Contains("Привет мир", dictationText);
    }

    [Fact]
    public async Task SpeechEngineCoordinator_MetaCommand_FiresMetaEvent()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice);

        string? metaCommand = null;
        coordinator.MetaCommandExecuted += (s, meta) => metaCommand = meta;

        var res = await coordinator.ProcessPhraseAsync("замолчи");
        Assert.True(res);
        Assert.Equal("замолчи", metaCommand);
    }

    [Fact]
    public async Task SpeechEngineCoordinator_ExecuteCommandAsync_DirectExecutionAndDuration()
    {
        var input = new MockInputSimulator();
        var windows = new MockWindowManager();
        var voice = new MockVoiceFeedback();
        using var coordinator = new SpeechEngineCoordinator(input, windows, voice);

        CommandMatchResult? matchResult = null;
        coordinator.CommandExecuted += (s, m) => matchResult = m;

        var cmd = new VoiceCommand
        {
            Name = "Тестовая пауза",
            Phrases = ["сделай паузу"],
            Actions = [new CommandAction(ActionType.Pause, "10", "")]
        };

        await coordinator.ExecuteCommandAsync(cmd);

        Assert.NotNull(matchResult);
        Assert.Equal("Тестовая пауза", matchResult.Command.Name);
        Assert.Equal("сделай паузу", matchResult.SpokenPhrase);
        Assert.True(matchResult.ExecutionDurationMs >= 0);
    }

    #endregion
}

