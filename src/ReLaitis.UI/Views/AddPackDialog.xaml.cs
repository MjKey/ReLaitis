using System.Windows;
using ReLaitis.Core.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace ReLaitis.UI.Views;

public partial class AddPackDialog : Window
{
    public CommandPack? CreatedPack { get; private set; }

    public AddPackDialog()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            PackNameBox.Focus();
            PackNameBox.SelectAll();
        };
    }

    private void CreateBtn_Click(object sender, RoutedEventArgs e)
    {
        var name = PackNameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            WpfMessageBox.Show("Введите название пакета.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var processFilter = ProcessFilterBox.Text?.Trim() ?? string.Empty;

        CreatedPack = new CommandPack
        {
            Name = name,
            ProcessFilter = processFilter,
            IsActive = true,
            Groups =
            [
                new CommandGroup
                {
                    Name = "Общие",
                    IsEnabled = true
                }
            ]
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
