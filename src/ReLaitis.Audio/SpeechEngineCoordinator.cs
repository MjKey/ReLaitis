using System.Text.RegularExpressions;
using ReLaitis.Audio.Capture;
using ReLaitis.Audio.Feedback;
using ReLaitis.Audio.Recognition;
using ReLaitis.Core.Engine;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Interfaces;
using ReLaitis.Core.Models;
using ReLaitis.Core.Storage;

namespace ReLaitis.Audio;

/// <summary>
/// Главный координатор голосового управления:
/// связывает захват звука, распознавание речи (STT), сопоставление команд, макросы, Wake Word и диктовку.
/// </summary>
public class SpeechEngineCoordinator : IDisposable
{
    private readonly AudioCaptureService _audioCapture;
    private readonly VoskSpeechRecognizer _voskRecognizer;
    private readonly WhisperSpeechRecognizer _whisperRecognizer;
    private readonly GoogleFullDuplexSpeechRecognizer _googleRecognizer;
    private readonly WindowsSapiSpeechRecognizer _sapiRecognizer;
    private ISpeechRecognizer _recognizer;

    private readonly CommandMatcher _matcher;
    private readonly ActionExecutor _executor;
    private readonly IInputSimulator _input;
    private readonly IWindowManager _windows;
    private readonly IVoiceFeedback _voice;
    private readonly AudioChimePlayer _chimes;

    public AudioCaptureService AudioCapture => _audioCapture;
    public VoskSpeechRecognizer VoskRecognizer => _voskRecognizer;
    public WhisperSpeechRecognizer WhisperRecognizer => _whisperRecognizer;
    public GoogleFullDuplexSpeechRecognizer GoogleRecognizer => _googleRecognizer;
    public WindowsSapiSpeechRecognizer SapiRecognizer => _sapiRecognizer;
    public SpeechEngineType CurrentEngineType { get; private set; } = SpeechEngineType.Vosk;

    public List<CommandPack> Packs { get; set; } = [];
    public Dictionary<string, string> GlobalVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IBrowserBridge? BrowserBridge { get; set; }

    public bool IsListening { get; private set; }
    public bool SoundFeedbackEnabled { get; set; } = true;

    public bool IsWakeWordEnabled { get; set; } = false;
    public string WakeWordPhrase { get; set; } = "лэйтис";
    public int WakeWordTimeoutSeconds { get; set; } = 6;
    public DateTime? WakeWordActiveUntil { get; private set; }
    public bool IsWakeWordActive => WakeWordActiveUntil.HasValue && DateTime.Now < WakeWordActiveUntil.Value;

    public bool IsDictationModeEnabled { get; private set; } = false;

    public event EventHandler<string>? PhraseRecognized;
    public event EventHandler<string>? PartialRecognized;
    public event EventHandler<CommandMatchResult>? CommandExecuted;
    public event EventHandler<float>? MicLevelChanged;
    public event EventHandler<string>? StatusChanged;
    public event Action? JetAimTriggered;
    public event Action<string, bool>? PackActivityToggled;
    public event Action<bool>? WakeWordStateChanged;
    public event Action<bool>? DictationModeChanged;
    public event EventHandler<string>? UnmatchedPhraseRecognized;
    public event EventHandler<string>? DictationRecognized;
    public event EventHandler<string>? MetaCommandExecuted;
    public Func<string, bool>? PhraseInterceptor;

    private TaskCompletionSource<string?>? _nextPhraseTcs;

    public SpeechEngineCoordinator(
        IInputSimulator input,
        IWindowManager windows,
        IVoiceFeedback voice)
    {
        _input = input;
        _windows = windows;
        _voice = voice;
        _chimes = new AudioChimePlayer();

        _audioCapture = new AudioCaptureService();
        _voskRecognizer = new VoskSpeechRecognizer();
        _whisperRecognizer = new WhisperSpeechRecognizer();
        _googleRecognizer = new GoogleFullDuplexSpeechRecognizer();
        _sapiRecognizer = new WindowsSapiSpeechRecognizer();
        _recognizer = _voskRecognizer;

        _matcher = new CommandMatcher();
        _executor = new ActionExecutor();

        // Загружаем сохраненные общие переменные
        var loadedVars = GlobalVariablesStorage.LoadVariables();
        foreach (var (k, v) in loadedVars)
            GlobalVariables[k] = v;

        _audioCapture.AudioLevelChanged += (s, level) => MicLevelChanged?.Invoke(this, level);
        _audioCapture.AudioDataAvailable += (s, data) => _recognizer.ProcessAudio(data);

        BindRecognizerEvents(_recognizer);
    }

    public void SaveGlobalVariables()
    {
        GlobalVariablesStorage.SaveVariables(GlobalVariables);
    }

    private void BindRecognizerEvents(ISpeechRecognizer recognizer)
    {
        recognizer.PartialResultRecognized += OnPartialResult;
        recognizer.FinalResultRecognized += OnFinalResult;
    }

    private void UnbindRecognizerEvents(ISpeechRecognizer recognizer)
    {
        recognizer.PartialResultRecognized -= OnPartialResult;
        recognizer.FinalResultRecognized -= OnFinalResult;
    }

    private void OnPartialResult(object? sender, string text)
    {
        PartialRecognized?.Invoke(this, text);
    }

    private async void OnFinalResult(object? sender, string text)
    {
        try
        {
            await HandlePhraseAsync(text);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Ошибка обработки фразы: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SpeechEngineCoordinator] OnFinalResult error: {ex}");
        }
    }

    public void SetEngine(SpeechEngineType type)
    {
        if (CurrentEngineType == type && _recognizer != null) return;

        if (_recognizer != null)
        {
            UnbindRecognizerEvents(_recognizer);
            _recognizer.Reset();
        }

        CurrentEngineType = type;
        _recognizer = type switch
        {
            SpeechEngineType.Whisper => _whisperRecognizer,
            SpeechEngineType.GoogleFullDuplex => _googleRecognizer,
            SpeechEngineType.WindowsSapi => _sapiRecognizer,
            _ => _voskRecognizer
        };
        BindRecognizerEvents(_recognizer);

        StatusChanged?.Invoke(this, $"Переключен движок речи: {type}");
    }

    public bool LoadVoskModel(string modelPath)
    {
        var loaded = _voskRecognizer.LoadModel(modelPath);
        StatusChanged?.Invoke(this, loaded ? "Vosk модель загружена" : "Ошибка загрузки модели Vosk");
        return loaded;
    }

    public bool LoadWhisperModel(string modelPath)
    {
        var loaded = _whisperRecognizer.LoadModel(modelPath);
        StatusChanged?.Invoke(this, loaded ? "Whisper модель загружена" : "Ошибка загрузки модели Whisper");
        return loaded;
    }

    public void StartListening()
    {
        if (IsListening) return;

        try
        {
            _audioCapture.StartCapture();
            IsListening = true;
            StatusChanged?.Invoke(this, "Прослушивание активно");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Ошибка микрофона: {ex.Message}");
        }
    }

    public void StopListening()
    {
        if (!IsListening) return;

        _audioCapture.StopCapture();
        _recognizer.Reset();
        IsListening = false;
        StatusChanged?.Invoke(this, "Прослушивание приостановлено");
    }

    public void ToggleListening()
    {
        if (IsListening)
            StopListening();
        else
            StartListening();
    }

    /// <summary>
    /// Выполняет команду по текстовой фразе (симуляция голоса или вызов из API).
    /// </summary>
    public async Task<bool> ProcessPhraseAsync(string phrase)
    {
        return await HandlePhraseAsync(phrase);
    }

    /// <summary>
    /// Напрямую выполняет указанную голосовую команду (например, повторный запуск из журнала истории).
    /// </summary>
    public async Task ExecuteCommandAsync(VoiceCommand command, CommandPack? pack = null, Dictionary<string, string>? variables = null)
    {
        var context = new MacroExecutionContext(
            _input,
            _windows,
            _voice,
            initialVariables: null,
            globalVariablesRef: GlobalVariables,
            browserBridge: BrowserBridge);

        if (pack != null)
        {
            foreach (var (k, v) in pack.Variables)
                context.Variables[k] = v;
        }

        if (variables != null)
        {
            foreach (var (k, v) in variables)
                context.Variables[k] = v;
        }

        context.VoiceCommandRequested += async (subCommandPhrase) =>
        {
            await HandlePhraseAsync(subCommandPhrase);
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _executor.ExecuteAsync(command.Actions, context);
        sw.Stop();

        var effectivePack = pack ?? Packs.FirstOrDefault(p => p.Groups.Any(g => g.Commands.Contains(command))) ?? new CommandPack { Name = "Общие" };
        var match = new CommandMatchResult(
            command,
            effectivePack,
            variables ?? [],
            1.0,
            command.Phrases.FirstOrDefault() ?? command.Name,
            sw.ElapsedMilliseconds);

        CommandExecuted?.Invoke(this, match);
    }

    public void PlayChime(ChimeType type)
    {
        if (SoundFeedbackEnabled)
            _chimes.PlayChime(type);
    }

    public void SetDictationMode(bool enabled)
    {
        IsDictationModeEnabled = enabled;
        DictationModeChanged?.Invoke(enabled);
        PlayChime(enabled ? ChimeType.ListeningStart : ChimeType.Error);
        StatusChanged?.Invoke(this, enabled ? "Режим диктовки включен (скажите 'выход из диктовки')" : "Режим диктовки выключен");
    }

    public string ApplyPackRules(string text, string? activeProcess = null)
    {
        if (string.IsNullOrWhiteSpace(text) || Packs.Count == 0)
            return text;

        var proc = activeProcess ?? _windows.GetActiveProcessName();
        var result = text;

        foreach (var pack in Packs)
        {
            if (!pack.IsActive || pack.Rules.Count == 0)
                continue;

            if (!string.IsNullOrEmpty(pack.ProcessFilter) &&
                !_windows.IsProcessActive(pack.ProcessFilter))
            {
                continue;
            }

            foreach (var rule in pack.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Word))
                    continue;

                var pattern = $@"\b{Regex.Escape(rule.Word)}\b";
                result = Regex.Replace(result, pattern, rule.Replacement ?? "", RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    private async Task<bool> HandlePhraseAsync(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return false;

        var activeProcess = _windows.GetActiveProcessName();
        phrase = ApplyPackRules(phrase, activeProcess);

        PhraseRecognized?.Invoke(this, phrase);

        // 1. Перехватчик фраз (например, когда активен интерактивный JetAim)
        if (PhraseInterceptor != null && PhraseInterceptor(phrase))
        {
            return true;
        }

        // 2. Если макрос ожидает следующую фразу пользователя (WaitNextCommand)
        if (_nextPhraseTcs != null && !_nextPhraseTcs.Task.IsCompleted)
        {
            _nextPhraseTcs.TrySetResult(phrase);
            return true;
        }

        var normalized = CommandMatcher.NormalizePhrase(phrase);

        // 3. Режим диктовки (Voice Dictation)
        if (IsDictationModeEnabled)
        {
            if (normalized is "выход из диктовки" or "отключи диктовку" or "стоп диктовка" or "закрыть диктовку")
            {
                SetDictationMode(false);
                return true;
            }

            var formattedText = DictationFormatter.FormatSpokenText(phrase);
            DictationRecognized?.Invoke(this, formattedText);
            _input.TypeText(formattedText + " ");
            return true;
        }

        if (normalized is "режим диктовки" or "включи диктовку" or "диктовка")
        {
            MetaCommandExecuted?.Invoke(this, phrase);
            SetDictationMode(true);
            return true;
        }

        // 4. Встроенные мета-команды быстрого прерывания и управления
        if (normalized is "замолчи" or "тихо" or "стоп голос" or "хватит говорить" or "заткнись" or "тишина")
        {
            MetaCommandExecuted?.Invoke(this, phrase);
            _voice.StopSpeaking();
            return true;
        }

        // 5. Режим фразы активации (Wake Word)
        if (IsWakeWordEnabled)
        {
            var wakeWords = (WakeWordPhrase ?? "лэйтис")
                .Split(new[] { ',', '/', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => CommandMatcher.NormalizePhrase(w.Trim()))
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            if (wakeWords.Count == 0)
                wakeWords.Add("лэйтис");

            // Проверяем: фраза точно совпадает с одним из ключевых слов (напр. "джарвис", "слушай", "эй ты", "лэйтис")
            var exactWakeWord = wakeWords.FirstOrDefault(w => string.Equals(normalized, w, StringComparison.OrdinalIgnoreCase));
            if (exactWakeWord != null)
            {
                WakeWordActiveUntil = DateTime.Now.AddSeconds(WakeWordTimeoutSeconds);
                WakeWordStateChanged?.Invoke(true);
                PlayChime(ChimeType.ListeningStart);
                StatusChanged?.Invoke(this, $"Слушаю команду ({WakeWordTimeoutSeconds}с)...");
                return true;
            }

            // Проверяем: фраза начинается с одного из ключевых слов (напр. "джарвис открой браузер", "слушай сколько времени")
            var prefixWakeWord = wakeWords.FirstOrDefault(w => normalized.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
            if (prefixWakeWord != null)
            {
                var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var wakeWordCount = prefixWakeWord.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (words.Length > wakeWordCount)
                {
                    phrase = string.Join(' ', words.Skip(wakeWordCount)).Trim();
                    normalized = CommandMatcher.NormalizePhrase(phrase);
                }

                WakeWordActiveUntil = DateTime.Now.AddSeconds(WakeWordTimeoutSeconds);
                WakeWordStateChanged?.Invoke(true);
            }
            else if (!IsWakeWordActive)
            {
                // Wake Word включен, окно не активно, фраза не содержит ключевого слова
                return false;
            }
        }

        activeProcess = _windows.GetActiveProcessName();
        var match = _matcher.FindMatch(
            phrase,
            Packs,
            activeProcess,
            filter => string.IsNullOrWhiteSpace(filter) || _windows.IsProcessActive(filter),
            proc => string.IsNullOrWhiteSpace(proc) || _windows.IsProcessRunning(proc));

        if (match != null)
        {
            if (string.IsNullOrEmpty(match.SpokenPhrase))
            {
                match = match with { SpokenPhrase = phrase };
            }
            PlayChime(ChimeType.Success);

            // Сбрасываем окно Wake Word после выполнения команды
            if (IsWakeWordEnabled)
            {
                WakeWordActiveUntil = null;
                WakeWordStateChanged?.Invoke(false);
            }

            // Формируем контекст выполнения макроса
            var context = new MacroExecutionContext(
                _input,
                _windows,
                _voice,
                initialVariables: null,
                globalVariablesRef: GlobalVariables,
                browserBridge: BrowserBridge);

            // Передаем переменные пакета
            foreach (var (k, v) in match.Pack.Variables)
                context.Variables[k] = v;

            // Передаем извлеченные из фразы слоты ({число}, {текст}, {app})
            foreach (var (k, v) in match.ExtractedVariables)
                context.Variables[k] = v;

            context.VoiceCommandRequested += async (subCommandPhrase) =>
            {
                await HandlePhraseAsync(subCommandPhrase);
            };

            context.TogglePackRequested += (packName, state) =>
            {
                var pack = Packs.FirstOrDefault(p => string.Equals(p.Name, packName, StringComparison.OrdinalIgnoreCase));
                if (pack != null)
                {
                    pack.IsActive = state switch
                    {
                        0 => !pack.IsActive, // 0: Toggle
                        1 => true,           // 1: Enable
                        2 => false,          // 2: Disable
                        _ => !pack.IsActive  // Default: Toggle
                    };
                    PackActivityToggled?.Invoke(pack.Name, pack.IsActive);
                }
            };

            context.ScheduleEventRequested += (delayMs, delayedPhrase) =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delayMs);
                    await HandlePhraseAsync(delayedPhrase);
                });
            };

            context.JetAimRequested += () =>
            {
                JetAimTriggered?.Invoke();
            };

            context.WaitForNextPhraseAsync = async (timeoutMs) =>
            {
                _nextPhraseTcs = new TaskCompletionSource<string?>();
                using var cts = new CancellationTokenSource(timeoutMs);
                using (cts.Token.Register(() => _nextPhraseTcs.TrySetResult(null)))
                {
                    try
                    {
                        return await _nextPhraseTcs.Task;
                    }
                    finally
                    {
                        _nextPhraseTcs = null;
                    }
                }
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _executor.ExecuteAsync(match.Command.Actions, context);
            sw.Stop();

            match = match with { ExecutionDurationMs = sw.ElapsedMilliseconds };
            CommandExecuted?.Invoke(this, match);
            return true;
        }

        if (IsWakeWordActive)
        {
            PlayChime(ChimeType.Error);
        }

        UnmatchedPhraseRecognized?.Invoke(this, phrase);
        return false;
    }

    public void Dispose()
    {
        StopListening();
        _audioCapture.Dispose();
        _voskRecognizer.Dispose();
        _whisperRecognizer.Dispose();
        _googleRecognizer.Dispose();
        _sapiRecognizer.Dispose();
        _chimes.Dispose();
    }
}
