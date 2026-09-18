using System.Runtime.InteropServices;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Interfaces;
using ReLaitis.Native.Win32;

namespace ReLaitis.Native;

/// <summary>
/// Аппаратный симулятор клавиатуры и мыши через Win32 SendInput.
/// Обеспечивает нулевую задержку отклика (<1 мс) и корректную работу в играх и Windows.
/// </summary>
public class WindowsInputSimulator : IInputSimulator
{
    public void SendHotkey(string keyCombination, ButtonAction action = ButtonAction.Press)
    {
        if (string.IsNullOrWhiteSpace(keyCombination))
            return;

        var parts = keyCombination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keys = new List<ushort>();

        foreach (var part in parts)
        {
            var vk = KeyCodes.ParseKey(part);
            if (vk != 0)
                keys.Add(vk);
        }

        if (keys.Count == 0)
            return;

        var inputs = new List<User32.INPUT>();

        // Если Press или Down - нажимаем все клавиши по очереди
        if (action is ButtonAction.Press or ButtonAction.Down)
        {
            foreach (var key in keys)
            {
                inputs.Add(CreateKeyInput(key, isKeyUp: false));
            }
        }

        // Если Press или Up - отпускаем все клавиши в обратном порядке
        if (action is ButtonAction.Press or ButtonAction.Up)
        {
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                inputs.Add(CreateKeyInput(keys[i], isKeyUp: true));
            }
        }

        if (inputs.Count > 0)
        {
            User32.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<User32.INPUT>());
        }
    }

    public void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var inputs = new List<User32.INPUT>();

        foreach (var ch in text)
        {
            if (ch == '\r') continue;

            // Нажатие и отпускание Unicode символа
            inputs.Add(CreateUnicodeInput(ch, isKeyUp: false));
            inputs.Add(CreateUnicodeInput(ch, isKeyUp: true));
        }

        if (inputs.Count > 0)
        {
            User32.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<User32.INPUT>());
        }
    }

    public void MoveMouse(int x, int y, MouseMoveType moveType, string? scribbleCoords = null)
    {
        switch (moveType)
        {
            case MouseMoveType.Point:
                User32.SetCursorPos(x, y);
                break;

            case MouseMoveType.Relative:
                SendRelativeMouseMove(x, y);
                break;

            case MouseMoveType.Scribble:
                if (!string.IsNullOrEmpty(scribbleCoords))
                {
                    var points = scribbleCoords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    for (var i = 0; i < points.Length - 1; i += 2)
                    {
                        if (int.TryParse(points[i], out var dx) && int.TryParse(points[i + 1], out var dy))
                        {
                            SendRelativeMouseMove(dx, dy);
                            Thread.Sleep(5);
                        }
                    }
                }
                break;
        }
    }

    public void MouseClick(int button, ButtonAction action = ButtonAction.Press)
    {
        var (downFlag, upFlag) = button switch
        {
            1 => (User32.MOUSEEVENTF_RIGHTDOWN, User32.MOUSEEVENTF_RIGHTUP),
            2 => (User32.MOUSEEVENTF_MIDDLEDOWN, User32.MOUSEEVENTF_MIDDLEUP),
            _ => (User32.MOUSEEVENTF_LEFTDOWN, User32.MOUSEEVENTF_LEFTUP)
        };

        var inputs = new List<User32.INPUT>();

        if (action is ButtonAction.Press or ButtonAction.Down)
        {
            inputs.Add(new User32.INPUT
            {
                type = User32.INPUT_MOUSE,
                u = new User32.InputUnion { mi = new User32.MOUSEINPUT { dwFlags = downFlag } }
            });
        }

        if (action is ButtonAction.Press or ButtonAction.Up)
        {
            inputs.Add(new User32.INPUT
            {
                type = User32.INPUT_MOUSE,
                u = new User32.InputUnion { mi = new User32.MOUSEINPUT { dwFlags = upFlag } }
            });
        }

        if (inputs.Count > 0)
        {
            User32.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<User32.INPUT>());
        }
    }

    public void MouseScroll(MouseScrollType scrollType, int delta)
    {
        var flag = scrollType == MouseScrollType.Horizontal
            ? User32.MOUSEEVENTF_HWHEEL
            : User32.MOUSEEVENTF_WHEEL;

        var input = new User32.INPUT
        {
            type = User32.INPUT_MOUSE,
            u = new User32.InputUnion
            {
                mi = new User32.MOUSEINPUT
                {
                    dwFlags = flag,
                    mouseData = (uint)delta
                }
            }
        };

        User32.SendInput(1, [input], Marshal.SizeOf<User32.INPUT>());
    }

    public (int X, int Y) GetMousePosition()
    {
        User32.GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }

    private static void SendRelativeMouseMove(int dx, int dy)
    {
        var input = new User32.INPUT
        {
            type = User32.INPUT_MOUSE,
            u = new User32.InputUnion
            {
                mi = new User32.MOUSEINPUT
                {
                    dwFlags = User32.MOUSEEVENTF_MOVE,
                    dx = dx,
                    dy = dy
                }
            }
        };
        User32.SendInput(1, [input], Marshal.SizeOf<User32.INPUT>());
    }

    private static User32.INPUT CreateKeyInput(ushort vk, bool isKeyUp)
    {
        uint flags = 0;
        if (isKeyUp) flags |= User32.KEYEVENTF_KEYUP;

        // Расширенные клавиши (стрелки, Del, Ins, Home, End, PageUp, PageDown)
        if (vk is KeyCodes.VK_LEFT or KeyCodes.VK_RIGHT or KeyCodes.VK_UP or KeyCodes.VK_DOWN
            or KeyCodes.VK_DELETE or KeyCodes.VK_INSERT or KeyCodes.VK_HOME or KeyCodes.VK_END
            or KeyCodes.VK_PRIOR or KeyCodes.VK_NEXT or KeyCodes.VK_LWIN or KeyCodes.VK_RWIN)
        {
            flags |= User32.KEYEVENTF_EXTENDEDKEY;
        }

        return new User32.INPUT
        {
            type = User32.INPUT_KEYBOARD,
            u = new User32.InputUnion
            {
                ki = new User32.KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = flags
                }
            }
        };
    }

    private static User32.INPUT CreateUnicodeInput(char c, bool isKeyUp)
    {
        uint flags = User32.KEYEVENTF_UNICODE;
        if (isKeyUp) flags |= User32.KEYEVENTF_KEYUP;

        return new User32.INPUT
        {
            type = User32.INPUT_KEYBOARD,
            u = new User32.InputUnion
            {
                ki = new User32.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = flags
                }
            }
        };
    }
}
