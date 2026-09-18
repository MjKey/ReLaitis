using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ReLaitis.Audio;
using ReLaitis.Audio.Capture;
using ReLaitis.Audio.Feedback;
using ReLaitis.Audio.Recognition;
using ReLaitis.Core.Engine;
using ReLaitis.Core.Models;
using ReLaitis.Core.Network;
using ReLaitis.Core.Storage;
using ReLaitis.Native;
using ReLaitis.Native.Win32;
using ReLaitis.UI.Views;
using WinForms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfButton = System.Windows.Controls.Button;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Helpers;
using ReLaitis.UI.Helpers;
using ReLaitis.UI.Models;

namespace ReLaitis.UI;

public partial class MainWindow : Window
{
    private CommandAction? SelectedAction =>
        (ActionsListBox.SelectedItem as ActionItemViewModel)?.Action
        ?? ActionsListBox.SelectedItem as CommandAction;

    private readonly WindowsInputSimulator _input;
    private readonly WindowsWindowManager _windows;
    private readonly TtsManager _ttsManager;
    private readonly SpeechEngineCoordinator _coordinator;
    private readonly GlobalHotkeyManager _hotkeyManager;
    private readonly BrowserBridgeServer _browserBridge;

    private readonly HudWindow _hudWindow;
    private readonly JetAimOverlayWindow _jetAimWindow;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly ObservableCollection<HistoryItem> _historyItems = [];
    private System.ComponentModel.ICollectionView? _historyView;
    private readonly ObservableCollection<GlobalVariableViewModel> _variableItems = [];

    private CommandPack? _selectedPack;
    private VoiceCommand? _selectedCommand;
    private bool _isUpdatingUi;
    private bool _isExiting;
    private UserSettings _settings;

    public MainWindow()
    {
        App.Diag("MainWindow constructor started");
        InitializeComponent();
        App.Diag("MainWindow InitializeComponent finished");

        System.Windows.Application.Current.SessionEnding += (s, e) =>
        {
            _isExiting = true;
        };

        _input = new WindowsInputSimulator();
        _windows = new WindowsWindowManager();
        _settings = SettingsStorage.LoadSettings();
        _ttsManager = new TtsManager(_settings);
        App.Diag("Core input/windows/ttsManager created");
        _coordinator = new SpeechEngineCoordinator(_input, _windows, _ttsManager);
        App.Diag("Coordinator created");
        _hotkeyManager = new GlobalHotkeyManager();
        App.Diag("HotkeyManager created");

        _browserBridge = new BrowserBridgeServer(11337);
        _coordinator.BrowserBridge = _browserBridge;

        _browserBridge.ConnectionChanged += isConnected =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateBrowserBridgeUi(isConnected);
            });
        };

        _browserBridge.PageStateChanged += (url, title) =>
        {
            Dispatcher.Invoke(() =>
            {
                LogStatusText.Text = $"Браузер: \"{title}\"";
            });
        };

        _browserBridge.LogMessage += msg =>
        {
            Dispatcher.Invoke(() =>
            {
                LogStatusText.Text = msg;
            });
        };

        _browserBridge.Start();
        App.Diag("BrowserBridge started");

        _hudWindow = new HudWindow();
        _hudWindow.ClosedByUser += () =>
        {
            _settings.ShowHud = false;
            SettingsStorage.SaveSettings(_settings);
            UpdateHudButtonUi(false);
        };
        _jetAimWindow = new JetAimOverlayWindow(_input);
        App.Diag("HudWindow and JetAimWindow created");

        _coordinator.JetAimTriggered += () =>
        {
            Dispatcher.Invoke(() => _jetAimWindow.ActivateJetAim());
        };

        _coordinator.PhraseInterceptor = phrase =>
        {
            var handled = false;
            Dispatcher.Invoke(() =>
            {
                handled = _jetAimWindow.HandlePhrase(phrase);
            });
            return handled;
        };

        // Настройка трея
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "ReLaitis - Голосовое управление"
        };

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Открыть ReLaitis", null, (s, e) => ShowAndRestore());
        contextMenu.Items.Add("Переключить прослушивание", null, (s, e) => ToggleListening());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Выход", null, (s, e) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowAndRestore();

        // Подписка на глобальные хоткеи и Push-to-Talk
        _hotkeyManager.PushToTalkStateChanged += isPressed =>
        {
            Dispatcher.Invoke(() =>
            {
                if (isPressed)
                {
                    _coordinator.StartListening();
                    UpdateListeningUi(true);
                }
                else
                {
                    _coordinator.StopListening();
                    UpdateListeningUi(false);
                }
            });
        };

        _hotkeyManager.ToggleHotkeyTriggered += () =>
        {
            Dispatcher.Invoke(ToggleListening);
        };

        _hotkeyManager.EscapeKeyPressed += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_jetAimWindow.IsJetAimActive)
                {
                    _jetAimWindow.CancelJetAim();
                    LogStatusText.Text = "JetAim отменен (клавиша Escape)";
                }
            });
        };

        _hotkeyManager.StartHook();
        App.Diag("HotkeyManager hook started");

        // Подписка на события движка
        _coordinator.MicLevelChanged += (s, level) =>
        {
            Dispatcher.Invoke(() =>
            {
                MainMicLevelBar.Value = Math.Clamp(level, 0, 1);
                _hudWindow.UpdateMicLevel(level);
            });
        };

        _coordinator.PartialRecognized += (s, text) =>
        {
            _hudWindow.ShowPartial(text);
        };

        _coordinator.PhraseRecognized += (s, phrase) =>
        {
            Dispatcher.Invoke(() =>
            {
                LogStatusText.Text = $"Услышано: \"{phrase}\"";
            });
        };

        _coordinator.CommandExecuted += (s, match) =>
        {
            Dispatcher.Invoke(() =>
            {
                LogStatusText.Text = $"Выполнено: \"{match.Command.Name}\"";
                var spoken = !string.IsNullOrWhiteSpace(match.SpokenPhrase)
                    ? match.SpokenPhrase
                    : (match.Command.Phrases.FirstOrDefault() ?? match.Command.Name);

                _hudWindow.ShowRecognized(spoken, match.Command.Name);

                var proc = _windows.GetActiveProcessName();
                var winTitle = _windows.GetActiveWindowTitle();
                var confText = $"{(int)(match.Confidence * 100)}%";

                var item = new HistoryItem
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    FullDateTime = DateTime.Now,
                    Phrase = spoken,
                    CommandName = match.Command.Name,
                    PackName = match.Pack?.Name ?? "-",
                    ConfidenceText = confText,
                    Confidence = match.Confidence,
                    Process = proc,
                    WindowTitle = winTitle,
                    DurationMs = match.ExecutionDurationMs,
                    Type = HistoryEntryType.Executed,
                    CommandRef = match.Command,
                    Actions = match.Command.Actions,
                    ExtractedVariables = match.ExtractedVariables != null
                        ? new Dictionary<string, string>(match.ExtractedVariables)
                        : []
                };

                AddHistoryItem(item);
            });
        };

        _coordinator.UnmatchedPhraseRecognized += (s, phrase) =>
        {
            Dispatcher.Invoke(() =>
            {
                var proc = _windows.GetActiveProcessName();
                var winTitle = _windows.GetActiveWindowTitle();

                var item = new HistoryItem
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    FullDateTime = DateTime.Now,
                    Phrase = phrase,
                    CommandName = "- (нет совпадений)",
                    PackName = "-",
                    ConfidenceText = "-",
                    Confidence = 0.0,
                    Process = proc,
                    WindowTitle = winTitle,
                    Type = HistoryEntryType.Unmatched
                };

                AddHistoryItem(item);
            });
        };

        _coordinator.DictationRecognized += (s, text) =>
        {
            Dispatcher.Invoke(() =>
            {
                var proc = _windows.GetActiveProcessName();
                var winTitle = _windows.GetActiveWindowTitle();

                var item = new HistoryItem
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    FullDateTime = DateTime.Now,
                    Phrase = text,
                    CommandName = "Ввод диктовки",
                    PackName = "Диктовка",
                    ConfidenceText = "100%",
                    Confidence = 1.0,
                    Process = proc,
                    WindowTitle = winTitle,
                    Type = HistoryEntryType.Dictation
                };

                AddHistoryItem(item);
            });
        };

        _coordinator.MetaCommandExecuted += (s, meta) =>
        {
            Dispatcher.Invoke(() =>
            {
                var proc = _windows.GetActiveProcessName();
                var winTitle = _windows.GetActiveWindowTitle();

                var item = new HistoryItem
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    FullDateTime = DateTime.Now,
                    Phrase = meta,
                    CommandName = $"Системная команда: {meta}",
                    PackName = "Система",
                    ConfidenceText = "100%",
                    Confidence = 1.0,
                    Process = proc,
                    WindowTitle = winTitle,
                    Type = HistoryEntryType.Meta
                };

                AddHistoryItem(item);
            });
        };

        _coordinator.StatusChanged += (s, status) =>
        {
            Dispatcher.Invoke(() =>
            {
                LogStatusText.Text = status;
            });
        };

        _coordinator.WakeWordStateChanged += isActive =>
        {
            Dispatcher.Invoke(() =>
            {
                if (isActive)
                {
                    _hudWindow.ShowPartial("Слушаю команду...");
                    ListeningPulseDot.Fill = new SolidColorBrush(MediaColor.FromRgb(245, 158, 11));
                    ListeningStatusLabel.Text = "Слушаю команду...";
                }
                else
                {
                    UpdateListeningUi(_coordinator.IsListening);
                }
            });
        };

        _coordinator.DictationModeChanged += isDictation =>
        {
            Dispatcher.Invoke(() =>
            {
                if (isDictation)
                {
                    _hudWindow.ShowRecognized("РЕЖИМ ДИКТОВКИ", "Скажите 'выход из диктовки'");
                    ListeningPulseDot.Fill = new SolidColorBrush(MediaColor.FromRgb(168, 85, 247));
                    ListeningStatusLabel.Text = "Режим диктовки";
                }
                else
                {
                    UpdateListeningUi(_coordinator.IsListening);
                }
            });
        };

        System.Windows.Application.Current.MainWindow = this;
        App.Diag("Application.Current.MainWindow assigned");

        // Применение пользовательских настроек
        ApplySettings();
        App.Diag("Settings applied");

        // Загрузка сохраненных пакетов ReLaitis или автоимпорт из Laitis
        LoadInitialPacks();
        App.Diag("Packs loaded");
        // Инициализация журнала
        _historyView = System.Windows.Data.CollectionViewSource.GetDefaultView(_historyItems);
        _historyView.Filter = HistoryFilterPredicate;
        HistoryListView.ItemsSource = _historyView;
        _historyItems.CollectionChanged += (s, e) =>
        {
            UpdateHistoryCountsAndKpis();
        };
        UpdateHistoryCountsAndKpis();

        // Инициализация общих переменных
        VariablesListView.ItemsSource = _variableItems;
        RefreshVariablesUi();

        Loaded += OnMainWindowLoaded;
        App.Diag("MainWindow constructor finished");
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        App.Diag("OnMainWindowLoaded started");
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        App.Diag($"MainWindow Handle: {handle}, Visibility: {Visibility}, WindowState: {WindowState}");

        if (_settings.ShowHud)
        {
            _hudWindow.Show();
            UpdateHudButtonUi(true);
        }
        else
        {
            UpdateHudButtonUi(false);
        }
        App.Diag("HudWindow state configured");

        // Автоматический старт прослушивания при запуске приложения
        if (_settings.ListeningMode != ListeningMode.PushToTalk)
        {
            _coordinator.StartListening();
            UpdateListeningUi(true);
        }
        else
        {
            UpdateListeningUi(false);
        }

        Activate();
        Focus();
        App.Diag("OnMainWindowLoaded completed");
    }

    private async void CheckAndInitModel()
    {
        if (_settings?.SpeechEngine == SpeechEngineType.Whisper)
        {
            var modelName = _settings.WhisperModel;
            if (WhisperModelManager.IsModelInstalled(modelName))
            {
                var modelPath = WhisperModelManager.GetModelPath(modelName);
                DownloadModelBtn.Content = $"Загрузка {modelName}...";
                DownloadModelBtn.IsEnabled = false;
                var loaded = await Task.Run(() => _coordinator.LoadWhisperModel(modelPath));
                if (loaded)
                {
                    _coordinator.SetEngine(SpeechEngineType.Whisper);
                    DownloadModelBtn.Content = $"Whisper";
                    DownloadModelBtn.ToolTip = $"Модель OpenAI Whisper ({modelName}) активна";
                    DownloadModelBtn.Background = new SolidColorBrush(MediaColor.FromRgb(5, 150, 105));
                    DownloadModelBtn.IsEnabled = false;
                }
            }
            else
            {
                DownloadModelBtn.Content = $"Загрузить Whisper";
                DownloadModelBtn.ToolTip = $"Нажмите для скачивания модели OpenAI Whisper ({modelName})";
                DownloadModelBtn.Background = new SolidColorBrush(MediaColor.FromRgb(217, 119, 6));
                DownloadModelBtn.IsEnabled = true;
            }
        }
        else
        {
            if (ModelManager.IsModelInstalled())
            {
                var modelPath = ModelManager.GetDefaultModelPath();
                DownloadModelBtn.Content = "Загрузка...";
                DownloadModelBtn.IsEnabled = false;
                var loaded = await Task.Run(() => _coordinator.LoadVoskModel(modelPath));
                if (loaded)
                {
                    _coordinator.SetEngine(SpeechEngineType.Vosk);
                    DownloadModelBtn.Content = "Vosk";
                    DownloadModelBtn.ToolTip = "Русская офлайн-модель Vosk активна";
                    DownloadModelBtn.Background = new SolidColorBrush(MediaColor.FromRgb(5, 150, 105));
                    DownloadModelBtn.IsEnabled = false;
                }
            }
            else
            {
                DownloadModelBtn.Content = "Загрузить Vosk";
                DownloadModelBtn.ToolTip = "Нажмите для загрузки русской модели Vosk (45 МБ)";
                DownloadModelBtn.Background = new SolidColorBrush(MediaColor.FromRgb(217, 119, 6));
                DownloadModelBtn.IsEnabled = true;
            }
        }
    }

    private async void DownloadModelBtn_Click(object sender, RoutedEventArgs e)
    {
        DownloadModelBtn.IsEnabled = false;
        DownloadModelBtn.Content = "Загрузка 0%...";

        var progress = new Progress<double>(percent =>
        {
            DownloadModelBtn.Content = $"Загрузка {(int)(percent * 100)}%...";
        });

        try
        {
            if (_settings.SpeechEngine == SpeechEngineType.Whisper)
            {
                var path = await WhisperModelManager.EnsureModelInstalledAsync(_settings.WhisperModel, progress);
                var loaded = _coordinator.LoadWhisperModel(path);
                if (loaded)
                {
                    _coordinator.SetEngine(SpeechEngineType.Whisper);
                    DownloadModelBtn.Content = "Whisper";
                    DownloadModelBtn.ToolTip = $"Модель OpenAI Whisper ({_settings.WhisperModel}) активна";
                    DownloadModelBtn.Background = new SolidColorBrush(MediaColor.FromRgb(5, 150, 105));
                    WpfMessageBox.Show($"Модель OpenAI Whisper ({_settings.WhisperModel}) успешно установлена и активирована!", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                var path = await ModelManager.EnsureModelInstalledAsync(progress);
                var loaded = _coordinator.LoadVoskModel(path);

                if (loaded)
                {
                    _coordinator.SetEngine(SpeechEngineType.Vosk);
                    DownloadModelBtn.Content = "Vosk";
                    DownloadModelBtn.ToolTip = "Русская офлайн-модель Vosk активна";
                    DownloadModelBtn.Background = new SolidColorBrush(MediaColor.FromRgb(5, 150, 105));
                    WpfMessageBox.Show("Русская офлайн-модель Vosk успешно установлена и подключена!", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            DownloadModelBtn.IsEnabled = true;
            DownloadModelBtn.Content = "Ошибка (повторить)";
            WpfMessageBox.Show($"Не удалось загрузить модель: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadInitialPacks()
    {
        if (PackStorage.HasSavedPacks())
        {
            try
            {
                var packs = PackStorage.LoadPacks();
                if (packs.Count > 0)
                {
                    _coordinator.Packs = packs;
                    RefreshPacksList();
                    LogStatusText.Text = $"Загружено {packs.Count} сохраненных пакетов ReLaitis";
                    return;
                }
            }
            catch (Exception ex)
            {
                LogStatusText.Text = $"Ошибка загрузки сохраненных пакетов: {ex.Message}";
            }
        }

        AutoLoadLaitisBackups();

        if (_coordinator.Packs.Count == 0)
        {
            CreateDefaultDemoPacks();
        }
    }

    private void CreateDefaultDemoPacks()
    {
        _coordinator.Packs = PackStorage.GetDefaultShowcasePacks();
        PackStorage.SavePacks(_coordinator.Packs);
        RefreshPacksList();
        LogStatusText.Text = $"Создан демонстрационный набор из {_coordinator.Packs.Count} пакетов ReLaitis";
    }

    private void AutoLoadLaitisBackups()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var laitisBackupDir = Path.Combine(localAppData, "Laitis", "SaveBackups");

        if (Directory.Exists(laitisBackupDir))
        {
            var latestFile = Directory.GetFiles(laitisBackupDir, "*.laitis")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();

            if (latestFile != null)
            {
                try
                {
                    var packs = LaitisImporter.ImportFromFile(latestFile, LaitisImporter.DefaultLaitisPassword);
                    _coordinator.Packs = packs;
                    RefreshPacksList();
                    LogStatusText.Text = $"Загружено {packs.Count} пакетов из Laitis ({Path.GetFileName(latestFile)})";
                }
                catch (Exception ex)
                {
                    LogStatusText.Text = $"Ошибка автозагрузки Laitis: {ex.Message}";
                }
            }
        }
    }

    private void RefreshPacksList()
    {
        PacksListBox.ItemsSource = null;
        PacksListBox.ItemsSource = _coordinator.Packs;
        if (_coordinator.Packs.Count > 0)
        {
            PacksListBox.SelectedIndex = 0;
        }
    }

    private void RefreshCommandsList()
    {
        if (_selectedPack != null)
        {
            var filter = SearchCommandsBox.Text?.Trim() ?? "";
            var commands = _selectedPack.Groups.SelectMany(g => g.Commands);
            if (!string.IsNullOrEmpty(filter))
            {
                commands = commands.Where(c =>
                    c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.Phrases.Any(p => p.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            }

            var list = commands.ToList();
            CommandsListBox.ItemsSource = list;
            if (list.Count > 0 && CommandsListBox.SelectedItem == null)
            {
                CommandsListBox.SelectedIndex = 0;
            }
        }
        else
        {
            CommandsListBox.ItemsSource = null;
        }
    }

    private void SearchCommandsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshCommandsList();
    }

    private void RefreshActionsList()
    {
        var prevSelectedIndex = ActionsListBox.SelectedIndex;
        ActionsListBox.ItemsSource = null;
        if (_selectedCommand != null)
        {
            var items = new List<ActionItemViewModel>();
            var depth = 0;
            for (var i = 0; i < _selectedCommand.Actions.Count; i++)
            {
                var action = _selectedCommand.Actions[i];
                var currentDepth = depth;

                if (action.Type == ActionType.EndBlock)
                {
                    depth = Math.Max(0, depth - 1);
                    currentDepth = depth;
                }
                else if (action.Type == ActionType.Else)
                {
                    currentDepth = Math.Max(0, depth - 1);
                }

                items.Add(new ActionItemViewModel(action, i, currentDepth));

                if (action.Type is ActionType.IfProcessSelected
                    or ActionType.IfProcessExists
                    or ActionType.IfVariableValue
                    or ActionType.IfWebsiteSelected
                    or ActionType.IfWebsiteNavValue
                    or ActionType.Loop
                    or ActionType.RandomActionBlock)
                {
                    depth++;
                }
            }

            ActionsListBox.ItemsSource = items;
            if (prevSelectedIndex >= 0 && prevSelectedIndex < items.Count)
                ActionsListBox.SelectedIndex = prevSelectedIndex;
        }
    }

    private void PacksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPack = PacksListBox.SelectedItem as CommandPack;
        RefreshCommandsList();
    }

    private void CommandsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCommand = CommandsListBox.SelectedItem as VoiceCommand;
        _isUpdatingUi = true;
        try
        {
            if (_selectedCommand != null)
            {
                CommandEmptyState.Visibility = Visibility.Collapsed;
                CommandEditorPanel.Visibility = Visibility.Visible;
                CommandNameEditorBox.Text = _selectedCommand.Name;
                CommandPhrasesBox.Text = string.Join(Environment.NewLine, _selectedCommand.Phrases);
                RefreshActionsList();
            }
            else
            {
                CommandEmptyState.Visibility = Visibility.Visible;
                CommandEditorPanel.Visibility = Visibility.Collapsed;
                CommandNameEditorBox.Text = string.Empty;
                CommandPhrasesBox.Text = string.Empty;
                RefreshActionsList();
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void CommandNameEditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUi || _selectedCommand == null) return;
        _selectedCommand.Name = CommandNameEditorBox.Text;
        CommandsListBox.Items.Refresh();
    }

    private void ApplyPhrasesBtn_Click(object sender, RoutedEventArgs e)
    {
        ApplyPhrases(true);
    }

    private void ApplyPhrases(bool showFeedback = false)
    {
        if (_selectedCommand == null) return;

        var phrases = (CommandPhrasesBox.Text ?? "")
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        _selectedCommand.Phrases = phrases;
        CommandsListBox.Items.Refresh();
        if (showFeedback)
        {
            LogStatusText.Text = $"Обновлены фразы для \"{_selectedCommand.Name}\" ({phrases.Count} шт.)";
        }
    }

    private void SavePacksBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PackStorage.SavePacks(_coordinator.Packs);
            LogStatusText.Text = $"Успешно сохранено {_coordinator.Packs.Count} пакетов в UserPacks.json";
            WpfMessageBox.Show("Конфигурация пакетов и команд успешно сохранена!", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddPackBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddPackDialog { Owner = this };
        if (dlg.ShowDialog() == true && dlg.CreatedPack != null)
        {
            _coordinator.Packs.Add(dlg.CreatedPack);
            RefreshPacksList();
            PacksListBox.SelectedItem = dlg.CreatedPack;
            LogStatusText.Text = $"Создан пакет: \"{dlg.CreatedPack.Name}\"";
        }
    }

    private void DeletePackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPack == null) return;

        var res = WpfMessageBox.Show($"Вы действительно хотите удалить пакет \"{_selectedPack.Name}\" со всеми его командами?",
            "Удаление пакета", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            _coordinator.Packs.Remove(_selectedPack);
            _selectedPack = null;
            RefreshPacksList();
            LogStatusText.Text = "Пакет удален";
        }
    }

    private void ExportPackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPack == null)
        {
            WpfMessageBox.Show("Сначала выберите пакет для экспорта.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Файлы пакетов ReLaitis (*.json)|*.json|Все файлы (*.*)|*.*",
            FileName = $"{_selectedPack.Name}.json",
            Title = "Экспорт пакета команд"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                PackStorage.SaveSinglePack(_selectedPack, saveDialog.FileName);
                LogStatusText.Text = $"Пакет \"{_selectedPack.Name}\" экспортирован";
                WpfMessageBox.Show($"Пакет успешно экспортирован в {saveDialog.FileName}", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportSinglePackBtn_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new WpfOpenFileDialog
        {
            Filter = "Файлы пакетов ReLaitis (*.json)|*.json|Все файлы (*.*)|*.*",
            Title = "Импорт пакета команд"
        };

        if (openDialog.ShowDialog() == true)
        {
            try
            {
                var pack = PackStorage.LoadSinglePack(openDialog.FileName);
                if (pack != null)
                {
                    _coordinator.Packs.Add(pack);
                    RefreshPacksList();
                    PacksListBox.SelectedItem = pack;
                    LogStatusText.Text = $"Импортирован пакет \"{pack.Name}\"";
                    WpfMessageBox.Show($"Пакет \"{pack.Name}\" успешно импортирован!", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void AddCommandBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPack == null)
        {
            WpfMessageBox.Show("Сначала выберите или создайте пакет команд слева.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new AddCommandDialog { Owner = this };
        if (dlg.ShowDialog() == true && dlg.CreatedCommand != null)
        {
            var group = _selectedPack.Groups.FirstOrDefault(g => string.Equals(g.Name, dlg.CategoryName, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new CommandGroup { Name = dlg.CategoryName };
                _selectedPack.Groups.Add(group);
            }

            group.Commands.Add(dlg.CreatedCommand);
            RefreshCommandsList();
            CommandsListBox.SelectedItem = dlg.CreatedCommand;
            LogStatusText.Text = $"Создана команда: \"{dlg.CreatedCommand.Name}\"";
        }
    }

    private void DeleteCommandBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPack == null || _selectedCommand == null) return;

        var res = WpfMessageBox.Show($"Удалить команду \"{_selectedCommand.Name}\"?", "Удаление команды", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            foreach (var group in _selectedPack.Groups)
            {
                group.Commands.Remove(_selectedCommand);
            }
            _selectedCommand = null;
            RefreshCommandsList();
            LogStatusText.Text = "Команда удалена";
        }
    }

    private void AddActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите или создайте команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new AddActionDialog { Owner = this };
        if (dlg.ShowDialog() == true && dlg.CreatedActions.Count > 0)
        {
            var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;
            foreach (var action in dlg.CreatedActions)
            {
                _selectedCommand.Actions.Insert(insertIndex++, action);
            }
            RefreshActionsList();
            ActionsListBox.SelectedIndex = dlg.CreatedActions.Count > 1 ? insertIndex - dlg.CreatedActions.Count : insertIndex - 1;
            LogStatusText.Text = $"Добавлено действий: {dlg.CreatedActions.Count}";
        }
    }

    private void QuickIfBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new AddActionDialog(initialType: ActionType.IfProcessSelected) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.CreatedActions.Count > 0)
        {
            var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;
            foreach (var action in dlg.CreatedActions)
            {
                _selectedCommand.Actions.Insert(insertIndex++, action);
            }
            RefreshActionsList();
            ActionsListBox.SelectedIndex = dlg.CreatedActions.Count > 1 ? insertIndex - dlg.CreatedActions.Count : insertIndex - 1;
            LogStatusText.Text = "Вставлен блок условия 'Если'";
        }
    }

    private void QuickElseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;
        _selectedCommand.Actions.Insert(insertIndex, new CommandAction(ActionType.Else));
        RefreshActionsList();
        ActionsListBox.SelectedIndex = insertIndex;
        LogStatusText.Text = "Вставлена ветка 'Иначе'";
    }

    private void QuickLoopBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new AddActionDialog(initialType: ActionType.Loop) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.CreatedActions.Count > 0)
        {
            var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;
            foreach (var action in dlg.CreatedActions)
            {
                _selectedCommand.Actions.Insert(insertIndex++, action);
            }
            RefreshActionsList();
            ActionsListBox.SelectedIndex = dlg.CreatedActions.Count > 1 ? insertIndex - dlg.CreatedActions.Count : insertIndex - 1;
            LogStatusText.Text = "Вставлен блок цикла 'Повторить N раз'";
        }
    }

    private void QuickBreakBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;
        _selectedCommand.Actions.Insert(insertIndex, new CommandAction(ActionType.Break));
        RefreshActionsList();
        ActionsListBox.SelectedIndex = insertIndex;
        LogStatusText.Text = "Вставлен 'Break' (прерывание цикла)";
    }

    private void QuickEndBlockBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;
        _selectedCommand.Actions.Insert(insertIndex, new CommandAction(ActionType.EndBlock));
        RefreshActionsList();
        ActionsListBox.SelectedIndex = insertIndex;
        LogStatusText.Text = "Вставлен 'Конец блока'";
    }

    private void AddActionByType(ActionType type, bool openDialog = true)
    {
        if (_selectedCommand == null)
        {
            WpfMessageBox.Show("Сначала выберите или создайте команду.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var insertIndex = ActionsListBox.SelectedIndex >= 0 ? ActionsListBox.SelectedIndex + 1 : _selectedCommand.Actions.Count;

        if (type is ActionType.Else or ActionType.EndBlock or ActionType.Break or ActionType.JetAim)
        {
            _selectedCommand.Actions.Insert(insertIndex, new CommandAction(type));
            RefreshActionsList();
            ActionsListBox.SelectedIndex = insertIndex;
            LogStatusText.Text = $"Добавлено действие: {type}";
            return;
        }

        if (openDialog)
        {
            var dlg = new AddActionDialog(initialType: type) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.CreatedActions.Count > 0)
            {
                foreach (var action in dlg.CreatedActions)
                {
                    _selectedCommand.Actions.Insert(insertIndex++, action);
                }
                RefreshActionsList();
                ActionsListBox.SelectedIndex = dlg.CreatedActions.Count > 1 ? insertIndex - dlg.CreatedActions.Count : insertIndex - 1;
                LogStatusText.Text = $"Добавлено действий: {dlg.CreatedActions.Count}";
            }
        }
        else
        {
            _selectedCommand.Actions.Insert(insertIndex, new CommandAction(type));
            RefreshActionsList();
            ActionsListBox.SelectedIndex = insertIndex;
            LogStatusText.Text = $"Добавлено действие: {type}";
        }
    }

    private ContextMenu CreateDarkContextMenu()
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42)), // #0F172A
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59)), // #1E293B
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };
        return menu;
    }

    private MenuItem CreateDarkMenuItem(string header, Action onClick)
    {
        var item = new MenuItem
        {
            Header = header,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249)), // #F1F5F9
            Background = System.Windows.Media.Brushes.Transparent,
            FontSize = 12,
            Padding = new Thickness(10, 6, 12, 6)
        };
        item.Click += (s, e) => onClick();
        return item;
    }

    private void AddConditionMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        var menu = CreateDarkContextMenu();

        menu.Items.Add(CreateDarkMenuItem("Окно активно (IfProcessSelected)...", () => AddActionByType(ActionType.IfProcessSelected)));
        menu.Items.Add(CreateDarkMenuItem("Процесс запущен (IfProcessExists)...", () => AddActionByType(ActionType.IfProcessExists)));
        menu.Items.Add(CreateDarkMenuItem("Значение переменной (IfVariableValue)...", () => AddActionByType(ActionType.IfVariableValue)));
        menu.Items.Add(CreateDarkMenuItem("Сайт открыт (IfWebsiteSelected)...", () => AddActionByType(ActionType.IfWebsiteSelected)));
        menu.Items.Add(new Separator { Background = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59)) });
        menu.Items.Add(CreateDarkMenuItem("Иначе (Else)", () => AddActionByType(ActionType.Else)));
        menu.Items.Add(CreateDarkMenuItem("Повторить N раз (Loop)...", () => AddActionByType(ActionType.Loop)));
        menu.Items.Add(CreateDarkMenuItem("Прервать цикл (Break)", () => AddActionByType(ActionType.Break)));
        menu.Items.Add(CreateDarkMenuItem("Случайный выбор (Random)", () => AddActionByType(ActionType.RandomActionBlock)));
        menu.Items.Add(CreateDarkMenuItem("Конец блока (EndBlock)", () => AddActionByType(ActionType.EndBlock)));

        menu.PlacementTarget = btn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AddActionMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        var menu = CreateDarkContextMenu();

        menu.Items.Add(CreateDarkMenuItem("Запустить программу / файл (OpenFile)...", () => AddActionByType(ActionType.OpenFile)));
        menu.Items.Add(CreateDarkMenuItem("Закрыть программу (CloseApp)...", () => AddActionByType(ActionType.CloseApp)));
        menu.Items.Add(CreateDarkMenuItem("Управление окном (ShowWindow)...", () => AddActionByType(ActionType.ShowWindow)));
        menu.Items.Add(CreateDarkMenuItem("Озвучить фразу (Say)...", () => AddActionByType(ActionType.Say)));
        menu.Items.Add(CreateDarkMenuItem("Воспроизвести звук (PlayAudio)...", () => AddActionByType(ActionType.PlayAudio)));
        menu.Items.Add(CreateDarkMenuItem("Открыть ссылку в браузере (OpenURL)...", () => AddActionByType(ActionType.OpenURL)));
        menu.Items.Add(CreateDarkMenuItem("Выполнить CMD / Batch скрипт...", () => AddActionByType(ActionType.BatchScript)));
        menu.Items.Add(CreateDarkMenuItem("Выполнить C# скрипт (CSharpScript)...", () => AddActionByType(ActionType.CSharpScript)));
        menu.Items.Add(CreateDarkMenuItem("HTTP вебхук / Умный дом (HttpWebRequest)...", () => AddActionByType(ActionType.HttpWebRequest)));

        menu.PlacementTarget = btn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AddUtilityMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        var menu = CreateDarkContextMenu();

        menu.Items.Add(CreateDarkMenuItem("Задать значение переменной (SetVariableValue)...", () => AddActionByType(ActionType.SetVariableValue)));
        menu.Items.Add(CreateDarkMenuItem("Ожидать ответ пользователя (WaitNextCommand)...", () => AddActionByType(ActionType.WaitNextCommand)));
        menu.Items.Add(CreateDarkMenuItem("Пауза / Задержка (Pause)...", () => AddActionByType(ActionType.Pause)));
        menu.Items.Add(CreateDarkMenuItem("Таймер / Отложенная команда (ScheduleEvent)...", () => AddActionByType(ActionType.ScheduleEvent)));
        menu.Items.Add(CreateDarkMenuItem("Всплывающее уведомление (Notify)...", () => AddActionByType(ActionType.Notify)));
        menu.Items.Add(CreateDarkMenuItem("Вкл/Выкл пакет команд (TogglePackActivity)...", () => AddActionByType(ActionType.TogglePackActivity)));

        menu.PlacementTarget = btn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AddInputMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        var menu = CreateDarkContextMenu();

        menu.Items.Add(CreateDarkMenuItem("Нажатие клавиш (Hotkeys)...", () => AddActionByType(ActionType.Hotkeys)));
        menu.Items.Add(CreateDarkMenuItem("Ввод текста (TypeText)...", () => AddActionByType(ActionType.TypeText)));
        menu.Items.Add(CreateDarkMenuItem("Навести курсор на элемент (MouseMoveOn)...", () => AddActionByType(ActionType.MouseMoveOn)));
        menu.Items.Add(CreateDarkMenuItem("Клик мыши (MouseButton)...", () => AddActionByType(ActionType.MouseButton)));
        menu.Items.Add(CreateDarkMenuItem("Переместить курсор (MouseMove)...", () => AddActionByType(ActionType.MouseMove)));
        menu.Items.Add(CreateDarkMenuItem("Колесико мыши (MouseScroll)...", () => AddActionByType(ActionType.MouseScroll)));
        menu.Items.Add(CreateDarkMenuItem("Координатная сетка (JetAim)", () => AddActionByType(ActionType.JetAim)));

        menu.PlacementTarget = btn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AddBrowserMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        var menu = CreateDarkContextMenu();

        menu.Items.Add(CreateDarkMenuItem("Клик по номеру / элементу (WebPageClick)...", () => AddActionByType(ActionType.WebPageClick)));
        menu.Items.Add(CreateDarkMenuItem("Перейти по адресу (WebPageNavigate)...", () => AddActionByType(ActionType.WebPageNavigate)));
        menu.Items.Add(CreateDarkMenuItem("Управление вкладками (WebPagePopupOpen)...", () => AddActionByType(ActionType.WebPagePopupOpen)));
        menu.Items.Add(CreateDarkMenuItem("Выполнить JavaScript (WebPageScript)...", () => AddActionByType(ActionType.WebPageScript)));

        menu.PlacementTarget = btn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // ==========================================
    // Управление Общими Переменными
    // ==========================================

    private void RefreshVariablesUi(string? filter = null)
    {
        _variableItems.Clear();
        var vars = _coordinator.GlobalVariables;

        IEnumerable<KeyValuePair<string, string>> query = vars;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(kvp =>
                kvp.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                kvp.Value.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var kvp in query.OrderBy(k => k.Key))
        {
            _variableItems.Add(new GlobalVariableViewModel(kvp.Key, kvp.Value));
        }

        VariablesCountText.Text = $"{_variableItems.Count} переменных";
    }

    private void SearchVariablesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshVariablesUi(SearchVariablesBox.Text);
    }

    private void AddVariableBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EditVariableDialog { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.VariableName))
        {
            _coordinator.GlobalVariables[dlg.VariableName] = dlg.VariableValue;
            _coordinator.SaveGlobalVariables();
            RefreshVariablesUi(SearchVariablesBox.Text);
            LogStatusText.Text = $"Создана переменная \"{dlg.VariableName}\"";
        }
    }

    private void SaveVariablesBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _coordinator.SaveGlobalVariables();
            LogStatusText.Text = $"Сохранено {_coordinator.GlobalVariables.Count} переменных";
            WpfMessageBox.Show("Общие переменные успешно сохранены!", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void VariablesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VariablesListView.SelectedItem is GlobalVariableViewModel vm)
        {
            EditVariable(vm);
        }
    }

    private void EditVariableItemBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GlobalVariableViewModel vm })
        {
            EditVariable(vm);
        }
    }

    private void EditVariable(GlobalVariableViewModel vm)
    {
        var dlg = new EditVariableDialog(vm.Key, vm.Value) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (!string.Equals(dlg.VariableName, vm.Key, StringComparison.OrdinalIgnoreCase))
            {
                _coordinator.GlobalVariables.Remove(vm.Key);
            }
            _coordinator.GlobalVariables[dlg.VariableName] = dlg.VariableValue;
            _coordinator.SaveGlobalVariables();
            RefreshVariablesUi(SearchVariablesBox.Text);
            LogStatusText.Text = $"Обновлена переменная \"{dlg.VariableName}\"";
        }
    }

    private void DeleteVariableItemBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GlobalVariableViewModel vm })
        {
            var res = WpfMessageBox.Show($"Удалить переменную \"{vm.Key}\"?", "Удаление переменной", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _coordinator.GlobalVariables.Remove(vm.Key);
                _coordinator.SaveGlobalVariables();
                RefreshVariablesUi(SearchVariablesBox.Text);
                LogStatusText.Text = $"Переменная \"{vm.Key}\" удалена";
            }
        }
    }

    private void CopyPlaceholderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GlobalVariableViewModel vm })
        {
            try
            {
                System.Windows.Clipboard.SetText(vm.Placeholder);
                LogStatusText.Text = $"Скопировано в буфер: {vm.Placeholder}";
            }
            catch (Exception ex)
            {
                LogStatusText.Text = $"Не удалось скопировать: {ex.Message}";
            }
        }
    }

    private void EditActionBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedAction = SelectedAction;
        if (_selectedCommand == null || selectedAction == null)
        {
            WpfMessageBox.Show("Сначала выберите действие для редактирования.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new AddActionDialog(selectedAction) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            RefreshActionsList();
            LogStatusText.Text = $"Обновлено действие: {selectedAction.Type}";
        }
    }

    private void ActionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedAction != null)
        {
            EditActionBtn_Click(sender, e);
        }
    }

    private void DeleteActionBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedAction = SelectedAction;
        if (_selectedCommand == null || selectedAction == null)
            return;

        var prevIndex = _selectedCommand.Actions.IndexOf(selectedAction);
        _selectedCommand.Actions.Remove(selectedAction);
        RefreshActionsList();
        if (_selectedCommand.Actions.Count > 0)
            ActionsListBox.SelectedIndex = Math.Min(prevIndex, _selectedCommand.Actions.Count - 1);
        LogStatusText.Text = "Действие удалено";
    }

    private void MoveActionUpBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedAction = SelectedAction;
        if (_selectedCommand == null || selectedAction == null)
            return;

        var idx = _selectedCommand.Actions.IndexOf(selectedAction);
        if (idx > 0)
        {
            _selectedCommand.Actions.RemoveAt(idx);
            _selectedCommand.Actions.Insert(idx - 1, selectedAction);
            RefreshActionsList();
            ActionsListBox.SelectedIndex = idx - 1;
        }
    }

    private void MoveActionDownBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedAction = SelectedAction;
        if (_selectedCommand == null || selectedAction == null)
            return;

        var idx = _selectedCommand.Actions.IndexOf(selectedAction);
        if (idx >= 0 && idx < _selectedCommand.Actions.Count - 1)
        {
            _selectedCommand.Actions.RemoveAt(idx);
            _selectedCommand.Actions.Insert(idx + 1, selectedAction);
            RefreshActionsList();
            ActionsListBox.SelectedIndex = idx + 1;
        }
    }

    private void MuteToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleListening();
    }

    private void ToggleListening()
    {
        _coordinator.ToggleListening();
        UpdateListeningUi(_coordinator.IsListening);
    }

    private void UpdateListeningUi(bool listening)
    {
        if (!listening)
        {
            ListeningPulseDot.Fill = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68)); // Красный
            ListeningStatusLabel.Text = "Микрофон на паузе";
            ListeningStatusLabel.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));
            MuteToggleBtn.Content = "Включить";
            MuteToggleBtn.Background = new SolidColorBrush(MediaColor.FromRgb(2, 132, 199));
            MuteToggleBtn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
            _hudWindow.SetListening(false);
            return;
        }

        MuteToggleBtn.Content = "Пауза";
        MuteToggleBtn.Background = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
        MuteToggleBtn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));

        if (_settings.ListeningMode == ListeningMode.PushToTalk)
        {
            ListeningPulseDot.Fill = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)); // Голубой
            ListeningStatusLabel.Text = $"Push-to-Talk ({_settings.PushToTalkKey})";
            ListeningStatusLabel.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));
        }
        else if (_settings.ListeningMode == ListeningMode.WakeWord)
        {
            ListeningPulseDot.Fill = new SolidColorBrush(MediaColor.FromRgb(245, 158, 11)); // Янтарный
            ListeningStatusLabel.Text = $"Wake Word: «{_settings.WakeWord}»";
            ListeningStatusLabel.Foreground = new SolidColorBrush(MediaColor.FromRgb(251, 191, 36));
        }
        else
        {
            ListeningPulseDot.Fill = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129)); // Зеленый
            ListeningStatusLabel.Text = "Слушаю постоянно";
            ListeningStatusLabel.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));
        }

        _hudWindow.SetListening(true);
    }

    private void UpdateHudButtonUi(bool isVisible)
    {
        if (isVisible)
        {
            ToggleHudBtn.Background = new SolidColorBrush(MediaColor.FromRgb(2, 132, 199));
            ToggleHudBtn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
            ToggleHudBtn.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        }
        else
        {
            ToggleHudBtn.Background = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
            ToggleHudBtn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
            ToggleHudBtn.Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
        }
    }

    private void ImportLaitisBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Filter = "Файлы Laitis (*.laitis;*.json)|*.laitis;*.json|Все файлы (*.*)|*.*",
            Title = "Выберите файл резервной копии Laitis"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var packs = LaitisImporter.ImportFromFile(dialog.FileName, LaitisImporter.DefaultLaitisPassword);
                _coordinator.Packs = packs;
                RefreshPacksList();
                WpfMessageBox.Show($"Успешно импортировано {packs.Count} пакетов команд!", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ToggleHudBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_hudWindow.IsVisible)
        {
            _hudWindow.Hide();
            _settings.ShowHud = false;
            UpdateHudButtonUi(false);
        }
        else
        {
            _hudWindow.Show();
            _settings.ShowHud = true;
            UpdateHudButtonUi(true);
        }
        SettingsStorage.SaveSettings(_settings);
    }

    private void ToggleJetAimBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_jetAimWindow.IsJetAimActive)
            _jetAimWindow.CancelJetAim();
        else
            _jetAimWindow.ActivateJetAim();
    }

    private void NavTab_Click(object sender, RoutedEventArgs e)
    {
        if (TabCollectionsRadio.IsChecked == true)
        {
            CollectionsPanel.Visibility = Visibility.Visible;
            VariablesPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Collapsed;
        }
        else if (TabVariablesRadio.IsChecked == true)
        {
            CollectionsPanel.Visibility = Visibility.Collapsed;
            VariablesPanel.Visibility = Visibility.Visible;
            SettingsPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Collapsed;
            RefreshVariablesUi(SearchVariablesBox.Text);
        }
        else if (TabSettingsRadio.IsChecked == true)
        {
            CollectionsPanel.Visibility = Visibility.Collapsed;
            VariablesPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Visible;
            HistoryPanel.Visibility = Visibility.Collapsed;
            PopulateSettingsUi();
        }
        else if (TabHistoryRadio.IsChecked == true)
        {
            CollectionsPanel.Visibility = Visibility.Collapsed;
            VariablesPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Visible;
            UpdateHistoryCountsAndKpis();
        }
    }

    private void UpdateBrowserBridgeUi(bool isConnected)
    {
        if (isConnected)
        {
            BrowserBridgeBtn.Content = "Браузер: В сети";
            BrowserBridgeBtn.Background = new SolidColorBrush(MediaColor.FromRgb(5, 150, 105));
            BrowserBridgeBtn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129));
            _hudWindow.ShowRecognized("Браузер подключен", "ReLaitis Voice Bridge активен");
        }
        else
        {
            BrowserBridgeBtn.Content = "Браузерное расширение (ReLaitis Extension)";
            BrowserBridgeBtn.Background = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
            BrowserBridgeBtn.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
        }
    }

    private void BrowserBridgeBtn_Click(object sender, RoutedEventArgs e)
    {
        var extPath = @"c:\Users\mjkey\Desktop\ReLaitis\src\ReLaitis.Extension";

        var statusMsg = _browserBridge.IsConnected
            ? $"Расширение браузера успешно подключено к ReLaitis!\n\nАктивная вкладка: {_browserBridge.CurrentTitle}\nURL: {_browserBridge.CurrentUrl}\nСервер: 127.0.0.1:11337"
            : $"Расширение пока не подключено к ReLaitis.\nЛокальный WebSocket сервер ожидает подключений на порту 11337.\n\nИнструкция по установке в Chrome / Edge / Яндекс.Браузер:\n1. Откройте страницу расширений (chrome://extensions или edge://extensions)\n2. Включите 'Режим разработчика'\n3. Нажмите 'Загрузить распакованное' и выберите папку расширения.\n\nОткрыть папку с расширением в Проводнике?";

        var res = WpfMessageBox.Show(statusMsg, "ReLaitis Browser Bridge",
            _browserBridge.IsConnected ? MessageBoxButton.OK : MessageBoxButton.YesNo,
            _browserBridge.IsConnected ? MessageBoxImage.Information : MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes && Directory.Exists(extPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = extPath,
                UseShellExecute = true
            });
        }
    }

    private void PopulateSettingsUi()
    {
        LoadSettingsDevices();
        LoadSettingsSapiVoices();

        // 1. STT Engine
        if (_settings.SpeechEngine == SpeechEngineType.Whisper)
        {
            WhisperEngineRadio.IsChecked = true;
            WhisperModelPanel.Visibility = Visibility.Visible;
        }
        else
        {
            VoskEngineRadio.IsChecked = true;
            WhisperModelPanel.Visibility = Visibility.Collapsed;
        }

        for (var i = 0; i < WhisperModelComboBox.Items.Count; i++)
        {
            if (WhisperModelComboBox.Items[i] is ComboBoxItem item &&
                item.Content?.ToString()?.StartsWith(_settings.WhisperModel, StringComparison.OrdinalIgnoreCase) == true)
            {
                WhisperModelComboBox.SelectedIndex = i;
                break;
            }
        }

        // 2. Listening mode
        switch (_settings.ListeningMode)
        {
            case ListeningMode.PushToTalk:
                PttRadio.IsChecked = true;
                break;
            case ListeningMode.WakeWord:
                WakeWordRadio.IsChecked = true;
                break;
            default:
                ContinuousRadio.IsChecked = true;
                break;
        }

        WakeWordBox.Text = _settings.WakeWord;
        WakeWordTimeoutBox.Text = _settings.WakeWordTimeoutSeconds.ToString();

        for (var i = 0; i < PttKeyComboBox.Items.Count; i++)
        {
            if (PttKeyComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), _settings.PushToTalkKey, StringComparison.OrdinalIgnoreCase))
            {
                PttKeyComboBox.SelectedIndex = i;
                break;
            }
        }
        if (PttKeyComboBox.SelectedIndex == -1)
            PttKeyComboBox.SelectedIndex = 0;

        // 3. TTS Provider
        switch (_settings.TtsProvider)
        {
            case TtsProviderType.WindowsSapi:
                SapiTtsRadio.IsChecked = true;
                break;
            case TtsProviderType.OpenAiTts:
                OpenAiTtsRadio.IsChecked = true;
                break;
            case TtsProviderType.MicrosoftEdgeNeural:
            default:
                EdgeTtsRadio.IsChecked = true;
                break;
        }
        TtsRadio_Checked(this, new RoutedEventArgs());

        // Edge voice
        for (var i = 0; i < EdgeVoiceComboBox.Items.Count; i++)
        {
            if (EdgeVoiceComboBox.Items[i] is ComboBoxItem item &&
                item.Content?.ToString()?.StartsWith(_settings.EdgeVoiceName, StringComparison.OrdinalIgnoreCase) == true)
            {
                EdgeVoiceComboBox.SelectedIndex = i;
                break;
            }
        }

        // OpenAI voice & API Key
        OpenAiApiKeyBox.Text = _settings.OpenAiApiKey;
        for (var i = 0; i < OpenAiVoiceComboBox.Items.Count; i++)
        {
            if (OpenAiVoiceComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), _settings.OpenAiVoice, StringComparison.OrdinalIgnoreCase))
            {
                OpenAiVoiceComboBox.SelectedIndex = i;
                break;
            }
        }

        SpeechRateSlider.Value = _settings.SpeechRate;

        // 4. Other settings
        SoundFeedbackCheckBox.IsChecked = _settings.SoundFeedbackEnabled;
        AutoStartCheckBox.IsChecked = WindowsAutoStartManager.IsAutoStartEnabled();
        ShowHudCheckBox.IsChecked = _settings.ShowHud;
        HudOpacitySlider.Value = _settings.HudOpacity;
        SettingsStatusText.Text = "";
    }

    private void LoadSettingsDevices()
    {
        var devices = AudioCaptureService.GetAvailableInputDevices();
        AudioDeviceComboBox.Items.Clear();

        if (devices.Count == 0)
        {
            AudioDeviceComboBox.Items.Add("Микрофоны не найдены");
            AudioDeviceComboBox.SelectedIndex = 0;
            return;
        }

        foreach (var (idx, name) in devices)
        {
            AudioDeviceComboBox.Items.Add($"[{idx}] {name}");
        }

        AudioDeviceComboBox.SelectedIndex = Math.Clamp(_settings.SelectedAudioDevice, 0, devices.Count - 1);
    }

    private void LoadSettingsSapiVoices()
    {
        var sapiVoices = WindowsVoiceFeedback.GetInstalledVoices();
        SapiVoiceComboBox.Items.Clear();

        if (sapiVoices.Count == 0)
        {
            SapiVoiceComboBox.Items.Add("Системные голоса не найдены");
            SapiVoiceComboBox.SelectedIndex = 0;
            return;
        }

        foreach (var voice in sapiVoices)
        {
            SapiVoiceComboBox.Items.Add(voice);
        }

        var selectedIdx = sapiVoices.FindIndex(v => string.Equals(v, _settings.SapiVoiceName, StringComparison.OrdinalIgnoreCase));
        SapiVoiceComboBox.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
    }

    private void EngineRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (WhisperModelPanel != null)
        {
            WhisperModelPanel.Visibility = WhisperEngineRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TtsRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (EdgeVoicePanel == null || SapiVoicePanel == null || OpenAiTtsPanel == null)
            return;

        EdgeVoicePanel.Visibility = EdgeTtsRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SapiVoicePanel.Visibility = SapiTtsRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        OpenAiTtsPanel.Visibility = OpenAiTtsRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private TtsManager? _testTtsManager;

    private async void TestTtsBtn_Click(object sender, RoutedEventArgs e)
    {
        TestTtsBtn.IsEnabled = false;
        TestTtsBtn.Content = "Озвучиваю...";

        try
        {
            var tempSettings = BuildSettingsFromUi();
            _testTtsManager?.Dispose();
            _testTtsManager = new TtsManager(tempSettings);

            await _testTtsManager.SpeakAsync("Привет! Я голосовой помощник ReLaitis. Готов к выполнению команд.");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Ошибка озвучки: {ex.Message}", "TTS Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            TestTtsBtn.IsEnabled = true;
            TestTtsBtn.Content = "Тест голоса";
        }
    }

    private UserSettings BuildSettingsFromUi()
    {
        var s = new UserSettings();

        // STT
        s.SpeechEngine = WhisperEngineRadio.IsChecked == true ? SpeechEngineType.Whisper : SpeechEngineType.Vosk;
        if (WhisperModelComboBox.SelectedItem is ComboBoxItem modelItem)
        {
            var content = modelItem.Content?.ToString() ?? "base";
            s.WhisperModel = content.Split(' ')[0].ToLowerInvariant();
        }

        s.SelectedAudioDevice = AudioDeviceComboBox.SelectedIndex >= 0 ? AudioDeviceComboBox.SelectedIndex : 0;

        if (PttRadio.IsChecked == true)
            s.ListeningMode = ListeningMode.PushToTalk;
        else if (WakeWordRadio.IsChecked == true)
            s.ListeningMode = ListeningMode.WakeWord;
        else
            s.ListeningMode = ListeningMode.Continuous;

        s.WakeWord = !string.IsNullOrWhiteSpace(WakeWordBox.Text) ? WakeWordBox.Text.Trim() : "лэйтис";
        if (int.TryParse(WakeWordTimeoutBox.Text, out var timeout))
            s.WakeWordTimeoutSeconds = Math.Clamp(timeout, 2, 60);

        if (PttKeyComboBox.SelectedItem is ComboBoxItem pttItem)
            s.PushToTalkKey = pttItem.Content?.ToString() ?? "CapsLock";

        // TTS
        if (SapiTtsRadio.IsChecked == true)
            s.TtsProvider = TtsProviderType.WindowsSapi;
        else if (OpenAiTtsRadio.IsChecked == true)
            s.TtsProvider = TtsProviderType.OpenAiTts;
        else
            s.TtsProvider = TtsProviderType.MicrosoftEdgeNeural;

        if (EdgeVoiceComboBox.SelectedItem is ComboBoxItem edgeItem)
        {
            var name = edgeItem.Content?.ToString() ?? "ru-RU-SvetlanaNeural";
            s.EdgeVoiceName = name.Split(' ')[0];
        }

        if (SapiVoiceComboBox.SelectedItem != null)
            s.SapiVoiceName = SapiVoiceComboBox.SelectedItem.ToString() ?? "";

        s.OpenAiApiKey = OpenAiApiKeyBox.Text?.Trim() ?? "";
        if (OpenAiVoiceComboBox.SelectedItem is ComboBoxItem aiVoiceItem)
            s.OpenAiVoice = aiVoiceItem.Content?.ToString() ?? "alloy";

        s.SpeechRate = (int)SpeechRateSlider.Value;

        // Feedback & HUD
        s.SoundFeedbackEnabled = SoundFeedbackCheckBox.IsChecked == true;
        s.AutoStartWithWindows = AutoStartCheckBox.IsChecked == true;
        s.ShowHud = ShowHudCheckBox.IsChecked == true;
        s.HudOpacity = HudOpacitySlider.Value;

        return s;
    }

    private void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings = BuildSettingsFromUi();

        // Сохраняем в реестр автозапуск Windows
        WindowsAutoStartManager.SetAutoStart(_settings.AutoStartWithWindows, true);

        // Сохраняем в JSON
        SettingsStorage.SaveSettings(_settings);

        // Применяем настройки
        ApplySettings();

        SettingsStatusText.Text = "Настройки успешно сохранены и применены!";
        LogStatusText.Text = "Настройки применены";
    }

    private void AddHistoryItem(HistoryItem item)
    {
        _historyItems.Insert(0, item);
        while (_historyItems.Count > 500)
        {
            _historyItems.RemoveAt(_historyItems.Count - 1);
        }
        UpdateHistoryCountsAndKpis();
    }

    private void UpdateHistoryCountsAndKpis()
    {
        if (HistoryTotalCountText == null) return;

        var total = _historyItems.Count;
        var executed = _historyItems.Count(i => i.Type == HistoryEntryType.Executed);
        var unmatched = _historyItems.Count(i => i.Type == HistoryEntryType.Unmatched);
        var scored = _historyItems.Where(i => i.Confidence > 0 && i.Type == HistoryEntryType.Executed).ToList();

        HistoryTotalCountText.Text = total.ToString();

        if (executed + unmatched > 0)
        {
            var rate = (int)Math.Round((double)executed / (executed + unmatched) * 100);
            HistorySuccessRateText.Text = $"{rate}% ({executed})";
        }
        else
        {
            HistorySuccessRateText.Text = "100% (0)";
        }

        HistoryUnmatchedCountText.Text = unmatched.ToString();

        if (scored.Count > 0)
        {
            var avgConf = (int)Math.Round(scored.Average(i => i.Confidence) * 100);
            HistoryAvgConfidenceText.Text = $"{avgConf}%";
        }
        else
        {
            HistoryAvgConfidenceText.Text = "-";
        }

        var visibleCount = _historyView?.Cast<object>().Count() ?? total;
        HistoryCountText.Text = visibleCount == total
            ? $"Записей: {total}"
            : $"Записей: {visibleCount} из {total}";
    }

    private void ApplyHistoryFilter()
    {
        _historyView?.Refresh();
        UpdateHistoryCountsAndKpis();
    }

    private bool HistoryFilterPredicate(object obj)
    {
        if (obj is not HistoryItem item) return false;

        if (HistoryFilterExecutedRadio?.IsChecked == true && item.Type != HistoryEntryType.Executed)
            return false;
        if (HistoryFilterUnmatchedRadio?.IsChecked == true && item.Type != HistoryEntryType.Unmatched)
            return false;
        if (HistoryFilterDictationRadio?.IsChecked == true && item.Type != HistoryEntryType.Dictation)
            return false;

        var query = HistorySearchBox?.Text?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            var matchesPhrase = item.Phrase.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchesCmd = item.CommandName.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchesPack = item.PackName.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchesProc = item.Process.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchesWin = item.WindowTitle.Contains(query, StringComparison.OrdinalIgnoreCase);
            var matchesVars = item.FormattedVariables.Contains(query, StringComparison.OrdinalIgnoreCase);

            if (!matchesPhrase && !matchesCmd && !matchesPack && !matchesProc && !matchesWin && !matchesVars)
                return false;
        }

        return true;
    }

    private void HistoryFilter_Checked(object sender, RoutedEventArgs e)
    {
        ApplyHistoryFilter();
    }

    private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyHistoryFilter();
    }

    private void ClearHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        _historyItems.Clear();
        HistoryEmptySelectionPanel.Visibility = Visibility.Visible;
        HistoryDetailContent.Visibility = Visibility.Collapsed;
        UpdateHistoryCountsAndKpis();
        LogStatusText.Text = "Журнал очищен";
    }

    private void HistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not HistoryItem item)
        {
            HistoryEmptySelectionPanel.Visibility = Visibility.Visible;
            HistoryDetailContent.Visibility = Visibility.Collapsed;
            return;
        }

        HistoryEmptySelectionPanel.Visibility = Visibility.Collapsed;
        HistoryDetailContent.Visibility = Visibility.Visible;

        DetailStatusBadge.Background = item.StatusBg;
        DetailStatusBadge.BorderBrush = item.StatusBorder;
        DetailStatusText.Text = item.StatusText;
        DetailStatusText.Foreground = item.StatusFg;

        DetailTimeText.Text = item.Time;
        DetailDurationText.Text = item.FormattedDuration;
        DetailPhraseText.Text = string.IsNullOrEmpty(item.Phrase) ? "-" : item.Phrase;

        DetailCommandText.Text = item.CommandName;
        DetailPackText.Text = $"Пакет: {item.PackName}";
        DetailConfidenceText.Text = item.ConfidenceText;
        DetailConfidenceText.Foreground = item.ConfidenceBrush;

        DetailProcessText.Text = string.IsNullOrEmpty(item.Process) ? "-" : item.Process;
        DetailWindowText.Text = string.IsNullOrEmpty(item.WindowTitle) ? "-" : item.WindowTitle;

        // Переменные / слоты
        if (item.HasVariables)
        {
            DetailVariablesSection.Visibility = Visibility.Visible;
            DetailVariablesText.Text = item.FormattedVariables;
        }
        else
        {
            DetailVariablesSection.Visibility = Visibility.Collapsed;
        }

        // Действия
        if (item.HasActions)
        {
            DetailActionsSection.Visibility = Visibility.Visible;
            var actionDisplays = item.Actions!.Select((a, idx) => new HistoryActionDisplay(
                $"#{idx + 1}",
                ActionVisuals.GetVisuals(a.Type).Label,
                string.IsNullOrWhiteSpace(a.DisplayDescription) ? a.Type.ToString() : a.DisplayDescription
            )).ToList();
            DetailActionsItemsControl.ItemsSource = actionDisplays;
        }
        else
        {
            DetailActionsSection.Visibility = Visibility.Collapsed;
            DetailActionsItemsControl.ItemsSource = null;
        }

        RepeatHistoryCommandBtn.IsEnabled = item.CommandRef != null || !string.IsNullOrWhiteSpace(item.Phrase);
    }

    private async void RepeatHistoryCommand_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not HistoryItem item) return;

        LogStatusText.Text = $"Повтор команды: {item.CommandName}";

        if (item.CommandRef != null)
        {
            var pack = _coordinator.Packs.FirstOrDefault(p => !string.IsNullOrEmpty(item.PackName) && string.Equals(p.Name, item.PackName, StringComparison.OrdinalIgnoreCase));
            await _coordinator.ExecuteCommandAsync(item.CommandRef, pack, item.ExtractedVariables);
            LogStatusText.Text = $"Команда \"{item.CommandName}\" повторно выполнена";
        }
        else if (!string.IsNullOrWhiteSpace(item.Phrase))
        {
            var res = await _coordinator.ProcessPhraseAsync(item.Phrase);
            if (!res)
            {
                LogStatusText.Text = $"Фраза \"{item.Phrase}\" не сопоставлена";
            }
        }
    }

    private void CreateCommandFromHistory_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not HistoryItem item) return;

        var phrase = item.Phrase?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(phrase))
        {
            WpfMessageBox.Show("У выбранного события нет фразы для создания команды.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pack = _coordinator.Packs.FirstOrDefault(p => !string.IsNullOrEmpty(item.PackName) && item.PackName != "-" && string.Equals(p.Name, item.PackName, StringComparison.OrdinalIgnoreCase))
                   ?? _selectedPack
                   ?? _coordinator.Packs.FirstOrDefault();

        if (pack == null)
        {
            pack = new CommandPack { Name = "Пользовательские команды" };
            _coordinator.Packs.Add(pack);
            RefreshPacksList();
        }

        var dlg = new AddCommandDialog(defaultCategory: "Общие", initialPhrase: phrase)
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.CreatedCommand != null)
        {
            var group = pack.Groups.FirstOrDefault(g => string.Equals(g.Name, dlg.CategoryName, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new CommandGroup { Name = dlg.CategoryName };
                pack.Groups.Add(group);
            }

            group.Commands.Add(dlg.CreatedCommand);

            PacksListBox.SelectedItem = pack;
            RefreshCommandsList();
            CommandsListBox.SelectedItem = dlg.CreatedCommand;

            TabCollectionsRadio.IsChecked = true;
            NavTab_Click(TabCollectionsRadio, new RoutedEventArgs());

            LogStatusText.Text = $"Создана команда \"{dlg.CreatedCommand.Name}\" из фразы \"{phrase}\"";
        }
    }

    private void CopyHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is not HistoryItem item) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{item.Time}] Статус: {item.StatusText}");
        sb.AppendLine($"Услышанная фраза: {item.Phrase}");
        sb.AppendLine($"Команда: {item.CommandName}");
        sb.AppendLine($"Пакет: {item.PackName}");
        sb.AppendLine($"Точность: {item.ConfidenceText}");
        sb.AppendLine($"Процесс: {item.Process}");
        if (!string.IsNullOrEmpty(item.WindowTitle) && item.WindowTitle != "-")
            sb.AppendLine($"Окно: {item.WindowTitle}");
        if (item.DurationMs > 0)
            sb.AppendLine($"Длительность выполнения: {item.DurationMs} мс");
        if (item.HasVariables)
            sb.AppendLine($"Переменные: {item.FormattedVariables}");
        if (item.HasActions)
            sb.AppendLine($"Действия: {item.ActionsSummary}");

        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
            LogStatusText.Text = "Сведения о событии скопированы в буфер обмена";
        }
        catch (Exception ex)
        {
            LogStatusText.Text = $"Ошибка копирования: {ex.Message}";
        }
    }

    private void CopyAllHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var items = _historyView?.Cast<HistoryItem>().ToList() ?? _historyItems.ToList();
        if (items.Count == 0)
        {
            WpfMessageBox.Show("Журнал пуст.", "ReLaitis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Время\tСтатус\tУслышанная фраза\tКоманда\tПакет\tТочность\tПроцесс\tОкно\tДлительность");
        foreach (var it in items)
        {
            sb.AppendLine($"{it.Time}\t{it.StatusText}\t{it.Phrase}\t{it.CommandName}\t{it.PackName}\t{it.ConfidenceText}\t{it.Process}\t{it.WindowTitle}\t{it.FormattedDuration}");
        }

        try
        {
            System.Windows.Clipboard.SetText(sb.ToString());
            LogStatusText.Text = $"Скопировано {items.Count} записей журнала в буфер обмена";
        }
        catch (Exception ex)
        {
            LogStatusText.Text = $"Ошибка копирования: {ex.Message}";
        }
    }

    private void ApplySettings()
    {
        _coordinator.AudioCapture.SetDevice(_settings.SelectedAudioDevice);
        _coordinator.IsWakeWordEnabled = _settings.ListeningMode == ListeningMode.WakeWord;
        _coordinator.WakeWordPhrase = _settings.WakeWord;
        _coordinator.WakeWordTimeoutSeconds = _settings.WakeWordTimeoutSeconds;
        _coordinator.SoundFeedbackEnabled = _settings.SoundFeedbackEnabled;
        _hotkeyManager.IsPushToTalkEnabled = _settings.ListeningMode == ListeningMode.PushToTalk;
        _hotkeyManager.PushToTalkVk = KeyCodes.ParseKey(_settings.PushToTalkKey);
        _hudWindow.Opacity = _settings.HudOpacity;
        _ttsManager.ApplySettings(_settings);

        CheckAndInitModel();

        if (_settings.ShowHud)
        {
            if (!_hudWindow.IsVisible) _hudWindow.Show();
            UpdateHudButtonUi(true);
        }
        else
        {
            if (_hudWindow.IsVisible) _hudWindow.Hide();
            UpdateHudButtonUi(false);
        }

        if (_settings.ListeningMode != ListeningMode.PushToTalk)
        {
            _coordinator.StartListening();
            UpdateListeningUi(true);
        }
        else
        {
            _coordinator.StopListening();
            UpdateListeningUi(false);
        }

        if (_coordinator.IsWakeWordEnabled)
        {
            LogStatusText.Text = $"Режим Wake Word активен (скажите \"{_settings.WakeWord}\")";
        }
    }

    private async void RunTestBtn_Click(object sender, RoutedEventArgs e)
    {
        var text = TestPhraseInput.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            LogStatusText.Text = $"Тест фразы: \"{text}\"";
            var success = await _coordinator.ProcessPhraseAsync(text);
            if (!success)
            {
                LogStatusText.Text = $"Команда для фразы \"{text}\" не найдена среди активных пакетов";
            }
        }
    }

    private void TestPhraseInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunTestBtn_Click(sender, e);
        }
    }

    private void ShowAndRestore()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _hotkeyManager.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _hudWindow.Close();
        _jetAimWindow.Close();
        _browserBridge.Dispose();
        _coordinator.Dispose();
        _ttsManager.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
        _notifyIcon.ShowBalloonTip(2000, "ReLaitis свернут в трей", "Голосовое управление продолжает работать в фоновом режиме.", WinForms.ToolTipIcon.Info);
    }
}