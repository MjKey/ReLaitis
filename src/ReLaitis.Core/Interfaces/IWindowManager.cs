using ReLaitis.Core.Enums;

namespace ReLaitis.Core.Interfaces;

/// <summary>
/// Интерфейс управления окнами и процессами Windows.
/// </summary>
public interface IWindowManager
{
    string GetActiveProcessName();
    string GetActiveWindowTitle();
    bool IsProcessRunning(string processName);
    bool IsProcessActive(string processName);
    bool StartProcess(string fileName, string arguments = "");
    bool CloseProcess(string processName, CloseAppBehaviour behaviour = CloseAppBehaviour.Close);
    bool SetWindowState(string processName, ShowWindowCommandType command);
    bool FindAndClickElementByName(string elementName, string actionName = "");
    string GetClipboardText();
}
