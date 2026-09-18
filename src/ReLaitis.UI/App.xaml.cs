using System.Configuration;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ReLaitis.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReLaitis", "startup_diag.log");

    public static void Diag(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenWindowStation(string lpszWinStation, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessWindowStation(IntPtr hWinStation);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint dwThreadId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool GetUserObjectInformationA(IntPtr hObj, int nIndex, [Out] byte[] pvInfo, int nLength, out int lpnLengthNeeded);

    public App()
    {
        EnsureInteractiveDesktop();
    }

    private static void EnsureInteractiveDesktop()
    {
        try
        {
            var hWinsta = OpenWindowStation("WinSta0", false, 0x00020000 | 0x037F);
            if (hWinsta != IntPtr.Zero)
            {
                SetProcessWindowStation(hWinsta);
            }
            var hDesk = OpenDesktop("Default", 0, false, 0x01FF);
            if (hDesk != IntPtr.Zero)
            {
                SetThreadDesktop(hDesk);
            }
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        EnsureInteractiveDesktop();

        Diag("App.OnStartup started");
        try
        {
            var hDesk = GetThreadDesktop(GetCurrentThreadId());
            byte[] buf = new byte[256];
            GetUserObjectInformationA(hDesk, 2, buf, buf.Length, out int needed);
            var deskName = System.Text.Encoding.ASCII.GetString(buf, 0, needed).TrimEnd('\0');
            Diag($"Thread Desktop: {deskName} (handle: {hDesk})");
        }
        catch (Exception ex)
        {
            Diag($"Desktop query error: {ex.Message}");
        }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Diag($"AppDomain Unhandled: {args.ExceptionObject}");
        };
        DispatcherUnhandledException += (s, args) =>
        {
            Diag($"Dispatcher Unhandled: {args.Exception}");
        };

        base.OnStartup(e);
        Diag("App.OnStartup finished base call");
    }
}
