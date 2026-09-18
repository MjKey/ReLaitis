using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReLaitis.UI.Models;

/// <summary>
/// Модель представления глобальной переменной для таблицы переменных в UI.
/// </summary>
public class GlobalVariableViewModel : INotifyPropertyChanged
{
    private string _key = string.Empty;
    private string _value = string.Empty;

    public string Key
    {
        get => _key;
        set
        {
            if (_key != value)
            {
                _key = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Placeholder));
            }
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }

    public string Placeholder => $"{{{Key}}}";

    public GlobalVariableViewModel() { }

    public GlobalVariableViewModel(string key, string value)
    {
        _key = key;
        _value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
