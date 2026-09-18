namespace ReLaitis.Core.Helpers;

/// <summary>
/// Правильное грамматическое склонение русских существительных с числительными.
/// </summary>
public static class RussianPluralizer
{
    public static string Format(int number, string one, string few, string many)
    {
        var abs = Math.Abs(number) % 100;
        var rem = abs % 10;
        if (abs is >= 11 and <= 19) return $"{number} {many}";
        if (rem == 1) return $"{number} {one}";
        if (rem is >= 2 and <= 4) return $"{number} {few}";
        return $"{number} {many}";
    }
}
