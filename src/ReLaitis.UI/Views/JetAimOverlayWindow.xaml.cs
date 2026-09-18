using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ReLaitis.Core.Engine;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Interfaces;

namespace ReLaitis.UI.Views;

public partial class JetAimOverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly IInputSimulator _input;
    private readonly List<int> _sectorStack = new();
    private double _deviceWidth;
    private double _deviceHeight;

    public bool IsJetAimActive => IsVisible;

    public JetAimOverlayWindow(IInputSimulator input)
    {
        InitializeComponent();
        _input = input;

        Top = SystemParameters.VirtualScreenTop;
        Left = SystemParameters.VirtualScreenLeft;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
        SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var m = source.CompositionTarget.TransformToDevice;
            _deviceWidth = Width * m.M11;
            _deviceHeight = Height * m.M22;
        }
        else
        {
            _deviceWidth = Width;
            _deviceHeight = Height;
        }
    }

    public void ActivateJetAim(string? presetSequence = null)
    {
        _sectorStack.Clear();
        SubGridContainer.Visibility = Visibility.Collapsed;
        InstructionText.Text = "JetAim: Назовите сектор 1-9 (или 0 = клик, 'назад', 'отмена')";
        Show();

        if (!string.IsNullOrWhiteSpace(presetSequence))
        {
            HandlePhrase(presetSequence);
        }
    }

    public void CancelJetAim()
    {
        _sectorStack.Clear();
        SubGridContainer.Visibility = Visibility.Collapsed;
        Hide();
    }

    private void CancelJetAimBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelJetAim();
    }

    private void Window_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CancelJetAim();
    }

    public bool HandlePhrase(string phrase)
    {
        if (!IsVisible || string.IsNullOrWhiteSpace(phrase))
            return false;

        var clean = phrase.Trim().ToLowerInvariant().Replace('ё', 'е');

        // Команды отмены
        if (clean is "отмена" or "отменить" or "стоп" or "закрыть" or "выход" or "esc")
        {
            CancelJetAim();
            return true;
        }

        // Команда назад (откат на 1 шаг)
        if (clean is "назад" or "шаг назад" or "откат" or "back")
        {
            StepBack();
            return true;
        }

        // Команды клика по центру
        if (clean is "0" or "ноль" or "клик" or "нажми" or "выбрать" or "центр")
        {
            ExecuteClickAtCurrentTarget(ButtonAction.Press, 0);
            CancelJetAim();
            return true;
        }

        if (clean is "правый клик" or "контекст" or "меню")
        {
            ExecuteClickAtCurrentTarget(ButtonAction.Press, 1);
            CancelJetAim();
            return true;
        }

        if (clean is "дважды" or "двойной клик")
        {
            ExecuteClickAtCurrentTarget(ButtonAction.Press, 0);
            Thread.Sleep(50);
            ExecuteClickAtCurrentTarget(ButtonAction.Press, 0);
            CancelJetAim();
            return true;
        }

        // Обработка цепочки цифр (напр. "528" или "пять два")
        var sequence = JetAimCalculator.ParseSectorSequence(clean);
        if (sequence.Count > 0)
        {
            foreach (var digit in sequence)
            {
                if (digit == 0)
                {
                    ExecuteClickAtCurrentTarget(ButtonAction.Press, 0);
                    CancelJetAim();
                    return true;
                }

                _sectorStack.Add(digit);
                if (_sectorStack.Count >= 5)
                {
                    ExecuteClickAtCurrentTarget(ButtonAction.Press, 0);
                    CancelJetAim();
                    return true;
                }
            }

            UpdateOverlayView();
            return true;
        }

        return false;
    }

    private void StepBack()
    {
        if (_sectorStack.Count > 0)
        {
            _sectorStack.RemoveAt(_sectorStack.Count - 1);
            UpdateOverlayView();
        }
        else
        {
            CancelJetAim();
        }
    }

    private void UpdateOverlayView()
    {
        if (_sectorStack.Count == 0)
        {
            SubGridContainer.Visibility = Visibility.Collapsed;
            InstructionText.Text = "JetAim: Назовите сектор 1-9 (или 'отмена')";
            return;
        }

        var (left, top, width, height) = JetAimCalculator.CalculateRecursiveBounds(_sectorStack, Width, Height);

        Canvas.SetLeft(SubGridContainer, left);
        Canvas.SetTop(SubGridContainer, top);
        SubGridContainer.Width = width;
        SubGridContainer.Height = height;
        SubGridContainer.Visibility = Visibility.Visible;

        var path = string.Join(" > ", _sectorStack);
        InstructionText.Text = $"Сектор [{path}]: уровень {_sectorStack.Count + 1}/5 (0 = клик, 'назад', 1-9)";
    }

    private void ExecuteClickAtCurrentTarget(ButtonAction action, int button)
    {
        var screenW = _deviceWidth > 0 ? _deviceWidth : Width;
        var screenH = _deviceHeight > 0 ? _deviceHeight : Height;

        var (targetX, targetY) = _sectorStack.Count > 0
            ? JetAimCalculator.CalculateRecursiveCenter(_sectorStack, screenW, screenH)
            : ((int)(screenW / 2), (int)(screenH / 2));

        var virtLeft = (int)SystemParameters.VirtualScreenLeft;
        var virtTop = (int)SystemParameters.VirtualScreenTop;

        _input.MoveMouse(virtLeft + targetX, virtTop + targetY, MouseMoveType.Point);
        _input.MouseClick(button, action);
    }
}
