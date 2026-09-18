using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace ReLaitis.Core.Engine;

/// <summary>
/// Движок динамической компиляции и исполнения C# скриптов в памяти через Roslyn.
/// Поддерживает кэширование делегатов для мгновенного повторного выполнения (0 мс) и асинхронность.
/// </summary>
public static class CSharpScriptEngine
{
    private static readonly ConcurrentDictionary<string, ScriptRunner<object?>> Cache = new();

    private static readonly ScriptOptions DefaultOptions = ScriptOptions.Default
        .WithImports(
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Threading.Tasks",
            "System.IO",
            "System.Net.Http",
            "System.Diagnostics",
            "System.Text.Json",
            "ReLaitis.Core.Models",
            "ReLaitis.Core.Interfaces",
            "ReLaitis.Core.Enums",
            "ReLaitis.Core.Engine"
        )
        .WithReferences(
            typeof(CSharpScriptGlobals).Assembly,
            typeof(object).Assembly,
            typeof(HttpClient).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(System.Text.RegularExpressions.Regex).Assembly
        );

    /// <summary>
    /// Выполняет C# код асинхронно с переданным контекстом глобальных переменных.
    /// </summary>
    /// <param name="code">C# код (выражение или последовательность инструкций, поддерживающих await).</param>
    /// <param name="globals">Глобальные объекты, доступные скрипту напрямую.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат выполнения (значение return или последнего выражения, либо null).</returns>
    public static async Task<object?> ExecuteAsync(
        string code,
        CSharpScriptGlobals globals,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmedCode = code.Trim();

        var runner = Cache.GetOrAdd(trimmedCode, c =>
        {
            var script = CSharpScript.Create<object?>(c, DefaultOptions, typeof(CSharpScriptGlobals));
            return script.CreateDelegate();
        });

        return await runner(globals, cancellationToken);
    }

    /// <summary>
    /// Очистить кэш скомпилированных скриптов.
    /// </summary>
    public static void ClearCache() => Cache.Clear();

    /// <summary>
    /// Количество скомпилированных скриптов в кэше.
    /// </summary>
    public static int CachedCount => Cache.Count;

    /// <summary>
    /// Проверить синтаксис кода без его выполнения.
    /// </summary>
    public static bool Validate(string code, out List<string> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(code))
            return true;

        try
        {
            var script = CSharpScript.Create<object?>(code, DefaultOptions, typeof(CSharpScriptGlobals));
            var diagnostics = script.Compile();
            var compilationErrors = diagnostics
                .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .Select(d => $"Строка {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}")
                .ToList();

            if (compilationErrors.Count > 0)
            {
                errors = compilationErrors;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            return false;
        }
    }
}
