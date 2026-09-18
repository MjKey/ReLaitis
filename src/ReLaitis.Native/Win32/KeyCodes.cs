namespace ReLaitis.Native.Win32;

public static class KeyCodes
{
    public const ushort VK_LBUTTON = 0x01;
    public const ushort VK_RBUTTON = 0x02;
    public const ushort VK_MBUTTON = 0x04;
    public const ushort VK_BACK = 0x08;
    public const ushort VK_TAB = 0x09;
    public const ushort VK_CLEAR = 0x0C;
    public const ushort VK_RETURN = 0x0D;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12; // ALT
    public const ushort VK_PAUSE = 0x13;
    public const ushort VK_CAPITAL = 0x14;
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_SPACE = 0x20;
    public const ushort VK_PRIOR = 0x21; // PageUp
    public const ushort VK_NEXT = 0x22;  // PageDown
    public const ushort VK_END = 0x23;
    public const ushort VK_HOME = 0x24;
    public const ushort VK_LEFT = 0x25;
    public const ushort VK_UP = 0x26;
    public const ushort VK_RIGHT = 0x27;
    public const ushort VK_DOWN = 0x28;
    public const ushort VK_SNAPSHOT = 0x2C; // PrintScreen
    public const ushort VK_INSERT = 0x2D;
    public const ushort VK_DELETE = 0x2E;
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_RWIN = 0x5C;
    public const ushort VK_APPS = 0x5D;

    // Медиа клавиши
    public const ushort VK_VOLUME_MUTE = 0xAD;
    public const ushort VK_VOLUME_DOWN = 0xAE;
    public const ushort VK_VOLUME_UP = 0xAF;
    public const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
    public const ushort VK_MEDIA_PREV_TRACK = 0xB1;
    public const ushort VK_MEDIA_STOP = 0xB2;
    public const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;

    private static readonly Dictionary<string, ushort> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = VK_CONTROL,
        ["Control"] = VK_CONTROL,
        ["Alt"] = VK_MENU,
        ["Shift"] = VK_SHIFT,
        ["Win"] = VK_LWIN,
        ["Windows"] = VK_LWIN,
        ["Enter"] = VK_RETURN,
        ["Return"] = VK_RETURN,
        ["Back"] = VK_BACK,
        ["Backspace"] = VK_BACK,
        ["Tab"] = VK_TAB,
        ["Esc"] = VK_ESCAPE,
        ["Escape"] = VK_ESCAPE,
        ["Space"] = VK_SPACE,
        ["PageUp"] = VK_PRIOR,
        ["PageDown"] = VK_NEXT,
        ["End"] = VK_END,
        ["Home"] = VK_HOME,
        ["Left"] = VK_LEFT,
        ["Up"] = VK_UP,
        ["Right"] = VK_RIGHT,
        ["Down"] = VK_DOWN,
        ["PrintScreen"] = VK_SNAPSHOT,
        ["PrtSc"] = VK_SNAPSHOT,
        ["Insert"] = VK_INSERT,
        ["Delete"] = VK_DELETE,
        ["Del"] = VK_DELETE,
        ["VolumeMute"] = VK_VOLUME_MUTE,
        ["VolumeDown"] = VK_VOLUME_DOWN,
        ["VolumeUp"] = VK_VOLUME_UP,
        ["Mute"] = VK_VOLUME_MUTE,
        ["MediaPlayPause"] = VK_MEDIA_PLAY_PAUSE,
        ["PlayPause"] = VK_MEDIA_PLAY_PAUSE,
        ["MediaNext"] = VK_MEDIA_NEXT_TRACK,
        ["MediaPrev"] = VK_MEDIA_PREV_TRACK
    };

    public static ushort ParseKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return 0;

        var clean = keyName.Trim();

        // 1. Поиск в словаре известных имен
        if (KeyMap.TryGetValue(clean, out var vk))
            return vk;

        // 2. F1 - F24
        if (clean.StartsWith('F') && int.TryParse(clean[1..], out var fNum) && fNum is >= 1 and <= 24)
        {
            return (ushort)(0x70 + (fNum - 1));
        }

        // 3. D0 - D9 (цифры)
        if (clean.StartsWith('D') && clean.Length == 2 && char.IsDigit(clean[1]))
        {
            return (ushort)('0' + (clean[1] - '0'));
        }

        if (clean.Length == 1)
        {
            var c = char.ToUpperInvariant(clean[0]);
            if (c is >= 'A' and <= 'Z')
                return (ushort)c;
            if (c is >= '0' and <= '9')
                return (ushort)c;
        }

        return 0;
    }
}
