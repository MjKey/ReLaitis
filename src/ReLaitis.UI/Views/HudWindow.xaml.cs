using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace ReLaitis.UI.Views;

public partial class HudWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public HudWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Делаем окно ненавязчивым (не забирает фокус у игр и полноэкранных окон)
        var helper = new WindowInteropHelper(this);
        var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
        SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Размещаем в правом верхнем углу рабочего стола
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Top + 20;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    public event Action? ClosedByUser;

    private void CloseHudBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        ClosedByUser?.Invoke();
    }

    public void UpdateMicLevel(float level)
    {
        Dispatcher.Invoke(() =>
        {
            MicLevelBar.Value = Math.Clamp(level, 0, 1);
        });
    }

    public void SetListening(bool isListening)
    {
        Dispatcher.Invoke(() =>
        {
            var color = isListening ? MediaColor.FromRgb(16, 185, 129) : MediaColor.FromRgb(239, 68, 68);
            var glowColor = isListening ? MediaColor.FromArgb(40, 16, 185, 129) : MediaColor.FromArgb(40, 239, 68, 68);

            StatusLed.Fill = new SolidColorBrush(color);
            StatusLedGlow.Fill = new SolidColorBrush(glowColor);
            ActionStatusText.Text = isListening ? "Слушаю..." : "Пауза";
        });
    }

    public void ShowPartial(string text)
    {
        Dispatcher.Invoke(() =>
        {
            RecognizedText.Text = text;
            RecognizedText.Foreground = new SolidColorBrush(MediaColor.FromRgb(156, 163, 175));
        });
    }

    public void ShowRecognized(string phrase, string? commandName)
    {
        Dispatcher.Invoke(() =>
        {
            RecognizedText.Text = phrase;
            RecognizedText.Foreground = new SolidColorBrush(MediaColor.FromRgb(243, 244, 246));
            ActionStatusText.Text = !string.IsNullOrEmpty(commandName)
                ? $"Выполнено: {commandName}"
                : "Команда не найдена";
        });
    }
}
