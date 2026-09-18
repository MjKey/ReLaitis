namespace ReLaitis.Core.Interfaces;

/// <summary>
/// Интерфейс взаимодействия с веб-браузером через локальный WebSocket сервер.
/// </summary>
public interface IBrowserBridge
{
    /// <summary>
    /// Подключено ли расширение браузера.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// URL текущей активной вкладки.
    /// </summary>
    string? CurrentUrl { get; }

    /// <summary>
    /// Заголовок текущей активной вкладки.
    /// </summary>
    string? CurrentTitle { get; }

    /// <summary>
    /// Событие изменения состояния подключения браузера.
    /// </summary>
    event Action<bool>? ConnectionChanged;

    /// <summary>
    /// Событие обновления активного URL или заголовка вкладки.
    /// </summary>
    event Action<string, string>? PageStateChanged;

    /// <summary>
    /// Клик по элементу (номер бейджа или CSS-селектор/текст).
    /// </summary>
    Task<bool> ClickElementAsync(string target, CancellationToken ct = default);

    /// <summary>
    /// Переход по указанному URL в активной вкладке.
    /// </summary>
    Task<bool> NavigateAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Прокрутка страницы (direction: "down", "up", "top", "bottom").
    /// </summary>
    Task<bool> ScrollAsync(string direction, int amount = 400, CancellationToken ct = default);

    /// <summary>
    /// Выполнение JavaScript в контексте активной страницы.
    /// </summary>
    Task<bool> ExecuteScriptAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Показ или скрытие номерных меток над интерактивными элементами.
    /// </summary>
    Task<bool> ToggleHintsAsync(bool show, CancellationToken ct = default);

    /// <summary>
    /// Получение текстового содержимого элемента по CSS-селектору.
    /// </summary>
    Task<string?> GetElementTextAsync(string selector, CancellationToken ct = default);

    /// <summary>
    /// Управление вкладками ("new", "close", "next", "prev", "reload", "back", "forward").
    /// </summary>
    Task<bool> TabActionAsync(string command, CancellationToken ct = default);
}
