using System.Diagnostics;
using System.Runtime.InteropServices;
using ReLaitis.Native.Win32;

namespace ReLaitis.Native;

/// <summary>
/// Глобальный менеджер горячих клавиш и режима Push-to-Talk (PTT).
/// Работает поверх любых приложений и полноэкранных игр.
/// </summary>
public class GlobalHotkeyManager : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;

    public ushort PushToTalkVk { get; set; } = KeyCodes.VK_CAPITAL; // По умолчанию CapsLock
    public bool IsPushToTalkEnabled { get; set; } = false;
    public bool IsPushToTalkPressed { get; private set; }

    public event Action<bool>? PushToTalkStateChanged;
    public event Action? ToggleHotkeyTriggered;
    public event Action? EscapeKeyPressed;

    public GlobalHotkeyManager()
    {
        _proc = HookCallback;
    }

    public void StartHook()
    {
        if (_hookId != IntPtr.Zero)
            return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    public void StopHook()
    {
        if (_hookId == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = (ushort)Marshal.ReadInt32(lParam);
            var isDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            var isUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

            // Обработка Escape
            if (isDown && vkCode == KeyCodes.VK_ESCAPE)
            {
                EscapeKeyPressed?.Invoke();
            }

            // Обработка Push-to-Talk
            if (IsPushToTalkEnabled && vkCode == PushToTalkVk)
            {
                if (isDown && !IsPushToTalkPressed)
                {
                    IsPushToTalkPressed = true;
                    PushToTalkStateChanged?.Invoke(true);
                }
                else if (isUp && IsPushToTalkPressed)
                {
                    IsPushToTalkPressed = false;
                    PushToTalkStateChanged?.Invoke(false);
                }
            }

            // Обработка горячей клавиши переключения (по умолчанию ScrollLock или F14..F24)
            if (isDown && vkCode == KeyCodes.ParseKey("ScrollLock"))
            {
                ToggleHotkeyTriggered?.Invoke();
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        StopHook();
    }
}
