using System.Windows;
using ReLaitis.Core.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace ReLaitis.UI.Views;

public partial class AddCommandDialog : Window
{
    public VoiceCommand? CreatedCommand { get; private set; }
    public string CategoryName { get; private set; } = "Общие";

    public AddCommandDialog(string defaultCategory = "Общие", string? initialPhrase = null)
    {
        InitializeComponent();
        CategoryBox.Text = string.IsNullOrWhiteSpace(defaultCategory) ? "Общие" : defaultCategory;
        if (!string.IsNullOrWhiteSpace(initialPhrase))
        {
            var trimmed = initialPhrase.Trim();
            CommandNameBox.Text = char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
            PhrasesBox.Text = trimmed;
        }
    }

    private void CreateBtn_Click(object sender, RoutedEventArgs e)
    {
        var name = CommandNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            WpfMessageBox.Show("Введите название команды.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CategoryName = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "Общие" : CategoryBox.Text.Trim();

        var phrasesText = PhrasesBox.Text ?? "";
        var phrases = phrasesText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (phrases.Count == 0)
        {
            phrases.Add(name.ToLowerInvariant());
        }

        CreatedCommand = new VoiceCommand
        {
            Name = name,
            IsEnabled = true,
            Phrases = phrases
        };

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
