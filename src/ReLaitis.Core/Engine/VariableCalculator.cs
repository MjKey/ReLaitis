using System.Globalization;
using System.Text.RegularExpressions;
using ReLaitis.Core.Enums;

namespace ReLaitis.Core.Engine;

/// <summary>
/// Вычислитель операций над переменными (арифметика, строки, случайные числа).
/// </summary>
public static class VariableCalculator
{
    public static string Calculate(string operand1, ArithmeticOperation op, string operand2)
    {
        operand1 = ResolveRandomTokens(operand1);
        if (!string.IsNullOrEmpty(operand2))
            operand2 = ResolveRandomTokens(operand2);

        // Если операция не задана (или передано фиктивное значение 15 для SetVariableValue)
        if (op == ArithmeticOperation.None || (int)op == 15)
        {
            return string.IsNullOrEmpty(operand2) ? EvaluateExpression(operand1) : operand1;
        }

        // Строковые операции
        if (op == ArithmeticOperation.Replace)
        {
            // Операнд 2 может содержать "find|replace" или просто строку
            var parts = operand2.Split('|', 2);
            if (parts.Length == 2)
                return operand1.Replace(parts[0], parts[1]);
            return operand1.Replace(operand2, string.Empty);
        }

        // Числовые операции
        var isNum1 = double.TryParse(operand1.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var num1);
        var isNum2 = double.TryParse(operand2.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var num2);

        if (isNum1)
        {
            switch (op)
            {
                case ArithmeticOperation.Add:
                    return isNum2 ? FormatDouble(num1 + num2) : operand1 + operand2;
                case ArithmeticOperation.Subtract:
                    return isNum2 ? FormatDouble(num1 - num2) : operand1;
                case ArithmeticOperation.Multiply:
                    return isNum2 ? FormatDouble(num1 * num2) : operand1;
                case ArithmeticOperation.Divide:
                    if (isNum2 && Math.Abs(num2) > double.Epsilon)
                        return FormatDouble(num1 / num2);
                    return "0";
                case ArithmeticOperation.Modulo:
                    if (isNum2 && Math.Abs(num2) > double.Epsilon)
                        return FormatDouble(num1 % num2);
                    return "0";
                case ArithmeticOperation.Power:
                    return isNum2 ? FormatDouble(Math.Pow(num1, num2)) : operand1;
                case ArithmeticOperation.Sqrt:
                    return num1 >= 0 ? FormatDouble(Math.Sqrt(num1)) : "0";
                case ArithmeticOperation.Round:
                    return isNum2 ? FormatDouble(Math.Round(num1, (int)num2)) : FormatDouble(Math.Round(num1));
                case ArithmeticOperation.Floor:
                    return FormatDouble(Math.Floor(num1));
                case ArithmeticOperation.Ceil:
                    return FormatDouble(Math.Ceiling(num1));
                case ArithmeticOperation.Abs:
                    return FormatDouble(Math.Abs(num1));
                case ArithmeticOperation.Random:
                    if (isNum2)
                    {
                        var min = (int)Math.Min(num1, num2);
                        var max = (int)Math.Max(num1, num2);
                        return Random.Shared.Next(min, max + 1).ToString();
                    }
                    return Random.Shared.Next(0, (int)num1 + 1).ToString();
            }
        }

        // Если это конкатенация строк при Add
        if (op == ArithmeticOperation.Add)
            return operand1 + operand2;

        return operand1;
    }

    public static bool EvaluateCondition(string left, VariableOperator op, string right)
    {
        var isNum1 = double.TryParse(left.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var num1);
        var isNum2 = double.TryParse(right.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var num2);

        if (isNum1 && isNum2)
        {
            return op switch
            {
                VariableOperator.Equals => Math.Abs(num1 - num2) < 1e-9,
                VariableOperator.NotEquals => Math.Abs(num1 - num2) >= 1e-9,
                VariableOperator.Greater => num1 > num2,
                VariableOperator.Less => num1 < num2,
                VariableOperator.GreaterOrEqual => num1 >= num2,
                VariableOperator.LessOrEqual => num1 <= num2,
                _ => false
            };
        }

        return op switch
        {
            VariableOperator.Equals => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            VariableOperator.NotEquals => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            VariableOperator.Contains => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            VariableOperator.NotContains => !left.Contains(right, StringComparison.OrdinalIgnoreCase),
            VariableOperator.StartsWith => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            VariableOperator.EndsWith => left.EndsWith(right, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string FormatDouble(double value)
    {
        // Если число целое - возвращаем без .0
        if (Math.Abs(value % 1) < 1e-9)
            return ((long)value).ToString();
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Вычисляет произвольное математическое выражение (например "10 + 5", "3 - 1", "{rnd:1:100}").
    /// Если строка не является математическим выражением, возвращает исходную строку.
    /// </summary>
    public static string EvaluateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        var expr = ResolveRandomTokens(expression.Trim());

        // Если это уже число - возвращаем как есть
        if (double.TryParse(expr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var directNum))
        {
            return FormatDouble(directNum);
        }

        try
        {
            var normalized = Regex.Replace(expr, @"(\d+),(\d+)", "$1.$2");
            using var dt = new System.Data.DataTable();
            var computed = dt.Compute(normalized, null);
            if (computed != null && double.TryParse(computed.ToString()?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var computedVal))
            {
                return FormatDouble(computedVal);
            }
        }
        catch
        {
            // Не является корректным математическим выражением (например, обычный текст)
        }

        return expr;
    }

    /// <summary>
    /// Преобразует динамические токены {rnd:min:max}, {rnd:max}, {random:min:max}, {rnd} в случайные целые числа.
    /// </summary>
    public static string ResolveRandomTokens(string input)
    {
        if (string.IsNullOrEmpty(input) || (!input.Contains("{rnd", StringComparison.OrdinalIgnoreCase) && !input.Contains("{random", StringComparison.OrdinalIgnoreCase)))
            return input;

        return Regex.Replace(input, @"\{(?:rnd|random)(?::(-?\d+)(?::(-?\d+))?)?\}", m =>
        {
            if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out var p1))
            {
                if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var p2))
                {
                    var min = Math.Min(p1, p2);
                    var max = Math.Max(p1, p2);
                    return Random.Shared.Next(min, max + 1).ToString();
                }
                else
                {
                    var min = p1 >= 0 ? 1 : p1;
                    var max = p1 >= 0 ? p1 : 0;
                    if (min > max) (min, max) = (max, min);
                    return Random.Shared.Next(min, max + 1).ToString();
                }
            }
            return Random.Shared.Next(1, 101).ToString();
        }, RegexOptions.IgnoreCase);
    }
}
