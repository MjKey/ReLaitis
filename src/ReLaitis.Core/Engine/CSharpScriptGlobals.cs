using ReLaitis.Core.Interfaces;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Engine;

/// <summary>
/// Глобальный контекст, передаваемый в выполняемый C# скрипт.
/// Предоставляет прямой доступ к инпуту, окнам, переменным, синтезу речи и управлению командами.
/// </summary>
public class CSharpScriptGlobals
{
    /// <summary>
    /// Полный контекст макроса ReLaitis.
    /// </summary>
    public MacroExecutionContext Context { get; }

    /// <summary>
    /// Симулятор ввода клавиатуры и мыши (SendKey, TypeText, MouseMove, MouseClick).
    /// </summary>
    public IInputSimulator Input => Context.Input;

    /// <summary>
    /// Управление окнами, процессами и буфером обмена Windows.
    /// </summary>
    public IWindowManager Windows => Context.Windows;

    /// <summary>
    /// Синтезатор речи (TTS).
    /// </summary>
    public IVoiceFeedback Voice => Context.Voice;

    /// <summary>
    /// Словарь локальных и глобальных переменных текущего макроса.
    /// </summary>
    public Dictionary<string, string> Variables => Context.Variables;

    /// <summary>
    /// Токен отмены выполнения макроса.
    /// </summary>
    public CancellationToken CancellationToken => Context.CancellationToken;

    public CSharpScriptGlobals(MacroExecutionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Получить значение переменной макроса по имени.
    /// </summary>
    public string? Get(string varName) => Context.GetVariable(varName);

    /// <summary>
    /// Записать значение переменной макроса.
    /// </summary>
    public void Set(string varName, object? value) =>
        Context.SetVariable(varName, value?.ToString() ?? string.Empty);

    /// <summary>
    /// Озвучить фразу через активный TTS движок ассистента (асинхронно).
    /// </summary>
    public Task SayAsync(string text) => Voice.SpeakAsync(text);

    /// <summary>
    /// Озвучить фразу через активный TTS движок ассистента (синхронно).
    /// </summary>
    public void Say(string text) => Voice.SpeakAsync(text).GetAwaiter().GetResult();

    /// <summary>
    /// Показать всплывающее экранное уведомление.
    /// </summary>
    public void Notify(string text, string title = "ReLaitis") =>
        Context.TriggerNotification(title, text);

    /// <summary>
    /// Запустить произвольную голосовую команду ассистента по её фразе.
    /// </summary>
    public void RunCommand(string commandPhrase) =>
        Context.RequestVoiceCommand(commandPhrase);

    /// <summary>
    /// Задержка выполнения (await Delay(ms)).
    /// </summary>
    public Task Delay(int milliseconds) =>
        Task.Delay(milliseconds, CancellationToken);

    /// <summary>
    /// Вывод отладочной строки в консоль/трассировку.
    /// </summary>
    public void Print(object? value) =>
        Console.WriteLine($"[C# Script]: {value}");
}
