using System.Windows;
using System.Windows.Controls;
using ReLaitis.Audio.Capture;
using ReLaitis.Audio.Feedback;
using ReLaitis.Audio.Synthesis;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;
using ReLaitis.Core.Storage;
using ReLaitis.Native;

namespace ReLaitis.UI.Views;

public partial class SettingsDialog : Window
{
    public UserSettings Settings { get; private set; }
    private TtsManager? _testTtsManager;

    public SettingsDialog(UserSettings? currentSettings = null)
    {
        InitializeComponent();

        Settings = currentSettings ?? SettingsStorage.LoadSettings();
        LoadDevices();
        LoadSapiVoices();
        PopulateUi();
    }

    private void LoadDevices()
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

        AudioDeviceComboBox.SelectedIndex = Math.Clamp(Settings.SelectedAudioDevice, 0, devices.Count - 1);
    }

    private void LoadSapiVoices()
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

        var selectedIdx = sapiVoices.FindIndex(v => string.Equals(v, Settings.SapiVoiceName, StringComparison.OrdinalIgnoreCase));
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

    private void PopulateUi()
    {
        // 1. STT Engine
        switch (Settings.SpeechEngine)
        {
            case SpeechEngineType.Whisper:
                WhisperEngineRadio.IsChecked = true;
                WhisperModelPanel.Visibility = Visibility.Visible;
                break;
            case SpeechEngineType.GoogleFullDuplex:
                GoogleDuplexEngineRadio.IsChecked = true;
                WhisperModelPanel.Visibility = Visibility.Collapsed;
                break;
            case SpeechEngineType.WindowsSapi:
                SapiEngineRadio.IsChecked = true;
                WhisperModelPanel.Visibility = Visibility.Collapsed;
                break;
            default:
                VoskEngineRadio.IsChecked = true;
                WhisperModelPanel.Visibility = Visibility.Collapsed;
                break;
        }

        for (var i = 0; i < WhisperModelComboBox.Items.Count; i++)
        {
            if (WhisperModelComboBox.Items[i] is ComboBoxItem item &&
                item.Content?.ToString()?.StartsWith(Settings.WhisperModel, StringComparison.OrdinalIgnoreCase) == true)
            {
                WhisperModelComboBox.SelectedIndex = i;
                break;
            }
        }

        // 2. Listening mode
        switch (Settings.ListeningMode)
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

        WakeWordBox.Text = Settings.WakeWord;
        WakeWordTimeoutBox.Text = Settings.WakeWordTimeoutSeconds.ToString();

        for (var i = 0; i < PttKeyComboBox.Items.Count; i++)
        {
            if (PttKeyComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), Settings.PushToTalkKey, StringComparison.OrdinalIgnoreCase))
            {
                PttKeyComboBox.SelectedIndex = i;
                break;
            }
        }
        if (PttKeyComboBox.SelectedIndex == -1)
            PttKeyComboBox.SelectedIndex = 0;

        // 3. TTS Provider
        switch (Settings.TtsProvider)
        {
            case TtsProviderType.GoogleChromium:
                GoogleChromiumTtsRadio.IsChecked = true;
                break;
            case TtsProviderType.BingTranslator:
                BingTranslatorTtsRadio.IsChecked = true;
                break;
            case TtsProviderType.VoiceRss:
                VoiceRssTtsRadio.IsChecked = true;
                break;
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
                item.Content?.ToString()?.StartsWith(Settings.EdgeVoiceName, StringComparison.OrdinalIgnoreCase) == true)
            {
                EdgeVoiceComboBox.SelectedIndex = i;
                break;
            }
        }

        // OpenAI voice & API Key
        OpenAiApiKeyBox.Text = Settings.OpenAiApiKey;
        for (var i = 0; i < OpenAiVoiceComboBox.Items.Count; i++)
        {
            if (OpenAiVoiceComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), Settings.OpenAiVoice, StringComparison.OrdinalIgnoreCase))
            {
                OpenAiVoiceComboBox.SelectedIndex = i;
                break;
            }
        }

        SpeechRateSlider.Value = Settings.SpeechRate;

        // 4. Other settings
        SoundFeedbackCheckBox.IsChecked = Settings.SoundFeedbackEnabled;
        AutoStartCheckBox.IsChecked = WindowsAutoStartManager.IsAutoStartEnabled();
        ShowHudCheckBox.IsChecked = Settings.ShowHud;
        HudOpacitySlider.Value = Settings.HudOpacity;
    }

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
            System.Windows.MessageBox.Show($"Ошибка озвучки: {ex.Message}", "TTS Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (WhisperEngineRadio.IsChecked == true)
            s.SpeechEngine = SpeechEngineType.Whisper;
        else if (GoogleDuplexEngineRadio.IsChecked == true)
            s.SpeechEngine = SpeechEngineType.GoogleFullDuplex;
        else if (SapiEngineRadio.IsChecked == true)
            s.SpeechEngine = SpeechEngineType.WindowsSapi;
        else
            s.SpeechEngine = SpeechEngineType.Vosk;

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
        if (GoogleChromiumTtsRadio.IsChecked == true)
            s.TtsProvider = TtsProviderType.GoogleChromium;
        else if (BingTranslatorTtsRadio.IsChecked == true)
            s.TtsProvider = TtsProviderType.BingTranslator;
        else if (VoiceRssTtsRadio.IsChecked == true)
            s.TtsProvider = TtsProviderType.VoiceRss;
        else if (SapiTtsRadio.IsChecked == true)
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

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        Settings = BuildSettingsFromUi();

        // Сохраняем в реестр автозапуск Windows
        WindowsAutoStartManager.SetAutoStart(Settings.AutoStartWithWindows, true);

        // Сохраняем в JSON
        SettingsStorage.SaveSettings(Settings);

        DialogResult = true;
        Close();
    }

    private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
    {
        VoiceAudioCache.ClearCache();
        System.Windows.MessageBox.Show("Кэш аудиозаписей озвучки успешно очищен.", "Очистка кэша", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _testTtsManager?.Dispose();
        base.OnClosed(e);
    }
}
