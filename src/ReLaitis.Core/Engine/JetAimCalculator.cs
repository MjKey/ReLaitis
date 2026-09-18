namespace ReLaitis.Core.Engine;

/// <summary>
/// Калькулятор координатной сетки JetAim для голосового управления курсором мыши.
/// Поддерживает расчет секторов 1-го уровня (3x3) и субсекторов 2-го уровня (3x3 внутри выбранного сектора).
/// </summary>
public static class JetAimCalculator
{
    public static (int X, int Y) CalculateSectorCenter(int sector, double screenWidth, double screenHeight)
    {
        sector = Math.Clamp(sector, 1, 9);
        var row = (sector - 1) / 3;
        var col = (sector - 1) % 3;

        var targetX = (int)((col + 0.5) * (screenWidth / 3.0));
        var targetY = (int)((row + 0.5) * (screenHeight / 3.0));

        return (targetX, targetY);
    }

    public static (int X, int Y) CalculateSubSectorCenter(int sector1, int sector2, double screenWidth, double screenHeight)
    {
        sector1 = Math.Clamp(sector1, 1, 9);
        sector2 = Math.Clamp(sector2, 1, 9);

        var row1 = (sector1 - 1) / 3;
        var col1 = (sector1 - 1) % 3;

        var sectorWidth = screenWidth / 3.0;
        var sectorHeight = screenHeight / 3.0;

        var left1 = col1 * sectorWidth;
        var top1 = row1 * sectorHeight;

        var row2 = (sector2 - 1) / 3;
        var col2 = (sector2 - 1) % 3;

        var subWidth = sectorWidth / 3.0;
        var subHeight = sectorHeight / 3.0;

        var targetX = (int)(left1 + (col2 + 0.5) * subWidth);
        var targetY = (int)(top1 + (row2 + 0.5) * subHeight);

        return (targetX, targetY);
    }

    public static (double Left, double Top, double Width, double Height) GetSectorBounds(int sector, double screenWidth, double screenHeight)
    {
        sector = Math.Clamp(sector, 1, 9);
        var row = (sector - 1) / 3;
        var col = (sector - 1) % 3;

        var sectorWidth = screenWidth / 3.0;
        var sectorHeight = screenHeight / 3.0;

        return (col * sectorWidth, row * sectorHeight, sectorWidth, sectorHeight);
    }

    public static (int X, int Y) CalculateRecursiveCenter(IEnumerable<int> sectors, double screenWidth, double screenHeight)
    {
        double curX = 0;
        double curY = 0;
        double curW = screenWidth;
        double curH = screenHeight;

        foreach (var sector in sectors)
        {
            var s = Math.Clamp(sector, 1, 9);
            curW /= 3.0;
            curH /= 3.0;
            curX += ((s - 1) % 3) * curW;
            curY += ((s - 1) / 3) * curH;
        }

        return ((int)(curX + curW / 2.0), (int)(curY + curH / 2.0));
    }

    public static (double Left, double Top, double Width, double Height) CalculateRecursiveBounds(IEnumerable<int> sectors, double screenWidth, double screenHeight)
    {
        double curX = 0;
        double curY = 0;
        double curW = screenWidth;
        double curH = screenHeight;

        foreach (var sector in sectors)
        {
            var s = Math.Clamp(sector, 1, 9);
            curW /= 3.0;
            curH /= 3.0;
            curX += ((s - 1) % 3) * curW;
            curY += ((s - 1) / 3) * curH;
        }

        return (curX, curY, curW, curH);
    }

    public static List<int> ParseSectorSequence(string input)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(input))
            return list;

        var clean = input.Trim().ToLowerInvariant().Replace('ё', 'е').Replace(",", "");

        // Прямой парсинг цепочки цифр (напр. "528", "52")
        var digitOnly = clean.Replace(" ", "");
        if (long.TryParse(digitOnly, out _))
        {
            foreach (var ch in digitOnly)
            {
                if (ch is >= '0' and <= '9')
                    list.Add(ch - '0');
            }
            return list;
        }

        // Составные числительные (например "пятьдесят два", "двадцать один")
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var w in words)
        {
            switch (w)
            {
                case "сто": list.Add(1); break;
                case "двести": list.Add(2); break;
                case "триста": list.Add(3); break;
                case "четыреста": list.Add(4); break;
                case "пятьсот": list.Add(5); break;
                case "шестьсот": list.Add(6); break;
                case "семьсот": list.Add(7); break;
                case "восемьсот": list.Add(8); break;
                case "девятьсот": list.Add(9); break;
                case "десять": list.Add(1); list.Add(0); break;
                case "одиннадцать": list.Add(1); list.Add(1); break;
                case "двенадцать": list.Add(1); list.Add(2); break;
                case "тринадцать": list.Add(1); list.Add(3); break;
                case "четырнадцать": list.Add(1); list.Add(4); break;
                case "пятнадцать": list.Add(1); list.Add(5); break;
                case "шестнадцать": list.Add(1); list.Add(6); break;
                case "семнадцать": list.Add(1); list.Add(7); break;
                case "восемнадцать": list.Add(1); list.Add(8); break;
                case "девятнадцать": list.Add(1); list.Add(9); break;
                case "двадцать": list.Add(2); break;
                case "тридцать": list.Add(3); break;
                case "сорок": list.Add(4); break;
                case "пятьдесят": list.Add(5); break;
                case "шестьдесят": list.Add(6); break;
                case "семьдесят": list.Add(7); break;
                case "восемьдесят": list.Add(8); break;
                case "девяносто": list.Add(9); break;
                case "ноль" or "центр": list.Add(0); break;
                default:
                    var parsed = ParseSectorNumber(w);
                    if (parsed.HasValue)
                        list.Add(parsed.Value);
                    break;
            }
        }

        return list;
    }

    public static int? ParseSectorNumber(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
            return null;

        var clean = phrase.Trim().ToLowerInvariant().Replace('ё', 'е');

        return clean switch
        {
            "0" or "ноль" => 0,
            "1" or "один" or "первый" or "раз" => 1,
            "2" or "два" or "второй" => 2,
            "3" or "три" or "третий" => 3,
            "4" or "четыре" or "четвертый" => 4,
            "5" or "пять" or "пятый" => 5,
            "6" or "шесть" or "шестой" => 6,
            "7" or "семь" or "седьмой" => 7,
            "8" or "восемь" or "восьмой" => 8,
            "9" or "девять" or "девятый" => 9,
            _ => null
        };
    }
}
