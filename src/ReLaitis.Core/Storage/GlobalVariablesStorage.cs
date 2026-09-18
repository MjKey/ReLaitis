using System.Text.Encodings.Web;
using System.Text.Json;

namespace ReLaitis.Core.Storage;

/// <summary>
/// Сервис локального сохранения и загрузки общих переменных (%LOCALAPPDATA%\ReLaitis\GlobalVariables.json).
/// </summary>
public static class GlobalVariablesStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string GetDefaultStoragePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReLaitis");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, "GlobalVariables.json");
    }

    public static bool HasSavedVariables(string? customPath = null)
    {
        var path = customPath ?? GetDefaultStoragePath();
        return File.Exists(path);
    }

    public static Dictionary<string, string> LoadVariables(string? customPath = null)
    {
        var path = customPath ?? GetDefaultStoragePath();
        if (!File.Exists(path))
        {
            // Стандартные начальные переменные
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["username"] = Environment.UserName,
                ["city"] = "Москва"
            };
        }

        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return dict != null
                ? new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SaveVariables(IDictionary<string, string> variables, string? customPath = null)
    {
        var path = customPath ?? GetDefaultStoragePath();
        var json = JsonSerializer.Serialize(variables, JsonOptions);
        File.WriteAllText(path, json);
    }
}
