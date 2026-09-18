using System.Collections.ObjectModel;
using System.Windows;
using ReLaitis.UI.Models;

namespace ReLaitis.UI.Views;

public partial class HistoryDialog : Window
{
    private readonly ObservableCollection<HistoryItem> _historyItems;

    public HistoryDialog(ObservableCollection<HistoryItem> items)
    {
        InitializeComponent();
        _historyItems = items;
        HistoryListView.ItemsSource = _historyItems;
    }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        _historyItems.Clear();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
