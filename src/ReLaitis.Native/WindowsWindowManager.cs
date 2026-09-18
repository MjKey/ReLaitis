using System.Diagnostics;
using System.Text;
using System.Windows.Automation;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Interfaces;
using ReLaitis.Native.Win32;

namespace ReLaitis.Native;

/// <summary>
/// Менеджер управления окнами и процессами Windows.
/// Отслеживает активные процессы для переключения профилей команд.
/// </summary>
public class WindowsWindowManager : IWindowManager
{
    public string GetActiveProcessName()
    {
        var hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        User32.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == 0)
            return string.Empty;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    public string GetActiveWindowTitle()
    {
        var hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        var sb = new StringBuilder(512);
        User32.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public bool IsProcessRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var parts = processName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(part =>
        {
            var clean = StripExe(part);
            var aliases = GetProcessAliases(clean);
            return aliases.Any(alias =>
            {
                var processes = Process.GetProcessesByName(alias);
                var isRunning = processes.Length > 0;
                foreach (var p in processes)
                    p.Dispose();
                return isRunning;
            });
        });
    }

    public bool IsProcessActive(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var activeProcess = GetActiveProcessName();
        var activeClean = StripExe(activeProcess);
        var activeTitle = GetActiveWindowTitle();

        // Особый случай для Windows 10/11 UWP: окно калькулятора может иметь процесс ApplicationFrameHost
        if (activeClean.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            if (activeTitle.Contains("Калькулятор", StringComparison.OrdinalIgnoreCase) ||
                activeTitle.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
            {
                activeClean = "calculator";
            }
        }

        var activeAliases = GetProcessAliases(activeClean);
        var parts = processName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Any(part =>
        {
            var cleanTarget = StripExe(part);
            var targetAliases = GetProcessAliases(cleanTarget);
            return targetAliases.Any(ta => activeAliases.Contains(ta, StringComparer.OrdinalIgnoreCase));
        });
    }

    public bool StartProcess(string fileName, string arguments = "")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CloseProcess(string processName, CloseAppBehaviour behaviour = CloseAppBehaviour.Close)
    {
        var clean = StripExe(processName);
        var processes = Process.GetProcessesByName(clean);
        if (processes.Length == 0)
            return false;

        var success = true;
        foreach (var p in processes)
        {
            try
            {
                if (behaviour == CloseAppBehaviour.Kill)
                {
                    p.Kill();
                }
                else
                {
                    p.CloseMainWindow();
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                p.Dispose();
            }
        }

        return success;
    }

    public bool SetWindowState(string processName, ShowWindowCommandType command)
    {
        var clean = StripExe(processName);
        var processes = Process.GetProcessesByName(clean);
        if (processes.Length == 0)
            return false;

        var cmdShow = command switch
        {
            ShowWindowCommandType.Minimize => User32.SW_MINIMIZE,
            ShowWindowCommandType.Maximize => User32.SW_MAXIMIZE,
            ShowWindowCommandType.Restore => User32.SW_RESTORE,
            ShowWindowCommandType.Normal => User32.SW_SHOWNORMAL,
            ShowWindowCommandType.Show => User32.SW_SHOW,
            _ => User32.SW_SHOWNORMAL
        };

        foreach (var p in processes)
        {
            if (p.MainWindowHandle != IntPtr.Zero)
            {
                User32.ShowWindow(p.MainWindowHandle, cmdShow);
                if (command is ShowWindowCommandType.Normal or ShowWindowCommandType.Maximize or ShowWindowCommandType.Restore)
                {
                    User32.SetForegroundWindow(p.MainWindowHandle);
                }
            }
            p.Dispose();
        }

        return true;
    }

    public bool FindAndClickElementByName(string elementName, string actionName = "")
    {
        if (string.IsNullOrWhiteSpace(elementName))
            return false;

        var cleanName = elementName.Trim().TrimStart('{').TrimEnd('}').Trim();
        if (string.IsNullOrWhiteSpace(cleanName))
            return false;

        try
        {
            // Собираем 3 ключевых корня окон без сканирования всего рабочего стола
            var rootHandles = new HashSet<IntPtr>();

            var fg = User32.GetForegroundWindow();
            if (fg != IntPtr.Zero)
                rootHandles.Add(fg);

            if (User32.GetCursorPos(out var pt))
            {
                var ptWnd = User32.WindowFromPoint(pt);
                if (ptWnd != IntPtr.Zero)
                    rootHandles.Add(ptWnd);
            }

            var tray = User32.FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero)
                rootHandles.Add(tray);

            var rootElements = new List<AutomationElement>();
            foreach (var h in rootHandles)
            {
                try
                {
                    var el = AutomationElement.FromHandle(h);
                    if (el != null)
                        rootElements.Add(el);
                }
                catch { }
            }

            // Ищем элемент параллельно по корням
            AutomationElement? targetElement = null;
            var searchTasks = rootElements.Select(root => Task.Run(() => SearchElementTree(root, cleanName, depth: 0, maxDepth: 6))).ToArray();
            Task.WaitAll(searchTasks, 1500);

            foreach (var t in searchTasks)
            {
                if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    targetElement = t.Result;
                    break;
                }
            }

            if (targetElement != null)
            {
                var rect = targetElement.Current.BoundingRectangle;
                if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                {
                    int targetX = (int)(rect.Left + rect.Width / 2);
                    int targetY = (int)(rect.Top + rect.Height / 2);
                    User32.SetCursorPos(targetX, targetY);

                    if (!string.IsNullOrEmpty(actionName))
                    {
                        var sim = new WindowsInputSimulator();
                        if (actionName == "3" || actionName.Contains("дабл", StringComparison.OrdinalIgnoreCase) || actionName.Contains("double", StringComparison.OrdinalIgnoreCase))
                        {
                            sim.MouseClick(0, ButtonAction.Press);
                            Thread.Sleep(50);
                            sim.MouseClick(0, ButtonAction.Press);
                        }
                        else if (actionName == "0" || actionName.Contains("клик", StringComparison.OrdinalIgnoreCase) || actionName.Contains("нажать", StringComparison.OrdinalIgnoreCase))
                        {
                            sim.MouseClick(0, ButtonAction.Press);
                        }
                        else if (actionName == "2" || actionName.Contains("контекст", StringComparison.OrdinalIgnoreCase) || actionName.Contains("прав", StringComparison.OrdinalIgnoreCase))
                        {
                            sim.MouseClick(1, ButtonAction.Press);
                        }
                    }

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FindAndClickElementByName] Error: {ex.Message}");
        }

        return false;
    }

    private static AutomationElement? SearchElementTree(AutomationElement current, string target, int depth, int maxDepth)
    {
        if (depth > maxDepth)
            return null;

        try
        {
            // Проверяем текущий элемент
            if (IsElementMatch(current, target))
                return current;

            // Поуровневый обход дочерних элементов через TreeScope.Children (без Descendants)
            var children = current.FindAll(TreeScope.Children, Condition.TrueCondition);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                try
                {
                    if (child.Current.IsOffscreen || !child.Current.IsEnabled)
                        continue;

                    if (IsElementMatch(child, target))
                        return child;

                    var foundInSubtree = SearchElementTree(child, target, depth + 1, maxDepth);
                    if (foundInSubtree != null)
                        return foundInSubtree;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    private static bool IsElementMatch(AutomationElement el, string target)
    {
        try
        {
            var name = el.Current.Name;
            if (MatchWordBoundary(name, target))
                return true;

            // Опрос LegacyIAccessiblePattern (10018): Name (30092), Value (30093), DefaultAction (30094)
            try
            {
                var legacyName = el.GetCurrentPropertyValue(AutomationProperty.LookupById(30092)) as string;
                if (MatchWordBoundary(legacyName, target))
                    return true;

                var legacyVal = el.GetCurrentPropertyValue(AutomationProperty.LookupById(30093)) as string;
                if (MatchWordBoundary(legacyVal, target))
                    return true;
            }
            catch { }

            var help = el.Current.HelpText;
            if (MatchWordBoundary(help, target))
                return true;

            var autoId = el.Current.AutomationId;
            if (!string.IsNullOrEmpty(autoId) && autoId.Equals(target, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Канонический алгоритм Laitis сопоставления границ слов (AM из 9m.cs).
    /// </summary>
    public static bool MatchWordBoundary(string? str, string target)
    {
        if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(target))
            return false;

        if (str.Length / target.Length > 20)
            return false;

        if (target.Length > 5 && str.StartsWith(target[..^2], StringComparison.OrdinalIgnoreCase))
            return true;

        int idx = str.IndexOf(target, StringComparison.OrdinalIgnoreCase);
        if (idx == -1)
            return false;

        int rightIdx = idx + target.Length;
        bool leftBoundary = idx == 0 || !char.IsLower(str[idx - 1]);
        bool rightBoundary = rightIdx == str.Length || !char.IsLower(str[rightIdx]);

        return leftBoundary && rightBoundary;
    }

    private static string StripExe(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return name[..^4];
        return name;
    }

    private static HashSet<string> GetProcessAliases(string processName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { processName };
        if (processName.Equals("calc", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("calculator", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("calculatorapp", StringComparison.OrdinalIgnoreCase))
        {
            set.Add("calc");
            set.Add("calculator");
            set.Add("calculatorapp");
            set.Add("ApplicationFrameHost");
        }
        return set;
    }

    public string GetClipboardText()
    {
        try
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
            }

            string result = string.Empty;
            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        result = System.Windows.Clipboard.GetText();
                    }
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(500);
            return result;
        }
        catch
        {
            return string.Empty;
        }
    }
}
