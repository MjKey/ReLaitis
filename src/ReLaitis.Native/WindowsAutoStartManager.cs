using Microsoft.Win32;

namespace ReLaitis.Native;

/// <summary>
/// Управление автозапуском приложения через реестр Windows (HKCU\Software\Microsoft\Windows\CurrentVersion\Run).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class WindowsAutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ReLaitis";

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enable, bool minimized = true)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var cmd = minimized ? $"\"{exePath}\" --minimized" : $"\"{exePath}\"";
                    key.SetValue(AppName, cmd);
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Игнорируем ограничения групповых политик Windows
        }
    }
}
