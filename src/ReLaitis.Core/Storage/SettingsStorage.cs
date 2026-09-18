using System.Text.Json;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Storage;

/// <summary>
/// Хранилище настроек приложения (%LOCALAPPDATA%\ReLaitis\UserSettings.json).
/// </summary>
public static class SettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "ReLaitis");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return Path.Combine(dir, "UserSettings.json");
    }

    public static UserSettings LoadSettings(string? filePath = null)
    {
        var path = filePath ?? GetDefaultSettingsPath();
        if (!File.Exists(path))
            return new UserSettings();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static void SaveSettings(UserSettings settings, string? filePath = null)
    {
        var path = filePath ?? GetDefaultSettingsPath();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }
}
