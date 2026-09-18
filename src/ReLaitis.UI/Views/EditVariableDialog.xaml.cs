using System.Text.RegularExpressions;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace ReLaitis.UI.Views;

public partial class EditVariableDialog : Window
{
    private readonly string? _originalKey;

    public string VariableName { get; private set; } = string.Empty;
    public string VariableValue { get; private set; } = string.Empty;

    public EditVariableDialog(string? key = null, string? value = null)
    {
        InitializeComponent();
        _originalKey = key;

        if (!string.IsNullOrWhiteSpace(key))
        {
            DialogTitle.Text = "Редактировать переменную";
            Title = "Редактировать переменную";
            VariableNameBox.Text = key;
            VariableValueBox.Text = value ?? string.Empty;
            UpdatePreview();
        }
        else
        {
            DialogTitle.Text = "Новая переменная";
            Title = "Создать переменную";
        }

        VariableNameBox.TextChanged += (s, e) => UpdatePreview();
        Loaded += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_originalKey))
                VariableNameBox.Focus();
            else
                VariableValueBox.Focus();
        };
    }

    private void UpdatePreview()
    {
        var clean = VariableNameBox.Text.Trim().Trim('{', '}');
        PlaceholderPreviewText.Text = string.IsNullOrEmpty(clean)
            ? "В макросах: {имя}"
            : $"В макросах: {{{clean}}}";
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var key = VariableNameBox.Text.Trim().Trim('{', '}');
        if (string.IsNullOrWhiteSpace(key))
        {
            WpfMessageBox.Show(
                "Пожалуйста, введите имя переменной.",
                "Ошибка ввода",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            VariableNameBox.Focus();
            return;
        }

        // Проверяем валидность имени (буквы, цифры, дефис, подчеркивание)
        if (!Regex.IsMatch(key, @"^[a-zA-Z0-9_\u0400-\u04FF\-]+$"))
        {
            WpfMessageBox.Show(
                "Имя переменной может содержать только буквы, цифры, дефис и подчеркивание.",
                "Недопустимые символы",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            VariableNameBox.Focus();
            return;
        }

        VariableName = key;
        VariableValue = VariableValueBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
