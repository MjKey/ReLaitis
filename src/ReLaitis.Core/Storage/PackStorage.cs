using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Storage;

/// <summary>
/// Сервис локального сохранения и загрузки пакетов команд ReLaitis (UserPacks.json).
/// Обеспечивает полностью человеко- и LLM-читаемый формат JSON (camelCase, понятные имена,
/// имена действий строками, поддержка прямых команд без групп, одиночных параметров и т.д.).
/// Сохраняет прозрачную поддержку импорта старых файлов Laitis.
/// </summary>
public static class PackStorage
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetDefaultStoragePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReLaitis");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "UserPacks.json");
    }

    public static bool HasSavedPacks()
    {
        return File.Exists(GetDefaultStoragePath());
    }

    /// <summary>
    /// Сохраняет список пакетов в читабельном формате JSON.
    /// </summary>
    public static void SavePacks(IEnumerable<CommandPack> packs, string? customPath = null)
    {
        var targetPath = customPath ?? GetDefaultStoragePath();
        var json = JsonSerializer.Serialize(packs, JsonOptions);
        File.WriteAllText(targetPath, json);
    }

    /// <summary>
    /// Загружает пакеты команд. Поддерживает новый читабельный формат, одиночный пак,
    /// и автоматически прозрачно мигрирует старый формат UserPacks.json.
    /// </summary>
    public static List<CommandPack> LoadPacks(string? customPath = null)
    {
        var targetPath = customPath ?? GetDefaultStoragePath();
        if (!File.Exists(targetPath))
            return [];

        var json = File.ReadAllText(targetPath);
        return ParsePacksJson(json, targetPath);
    }

    /// <summary>
    /// Сохраняет один пакет в файл (для экспорта / обмена).
    /// </summary>
    public static void SaveSinglePack(CommandPack pack, string filePath)
    {
        var json = JsonSerializer.Serialize(pack, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Загружает один пакет из файла (поддерживает как одиночный пак, так и список из одного пака).
    /// </summary>
    public static CommandPack? LoadSinglePack(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        var packs = ParsePacksJson(json, null);
        return packs.Count > 0 ? packs[0] : null;
    }

    /// <summary>
    /// Универсальный парсер JSON пакетов команд с поддержкой всех форматов (чистый JSON, массив, одиночный пак, старый Laitis).
    /// </summary>
    public static List<CommandPack> ParsePacksJson(string json, string? saveMigratedPath = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1. Корневой элемент - массив пакетов
            if (root.ValueKind == JsonValueKind.Array)
            {
                // Проверяем, не старый ли это Laitis-формат ("N", "P", "C" вместо "name", "groups")
                if (IsLegacyLaitisFormat(root))
                {
                    var legacyPacks = LaitisImporter.ImportFromJson(json);
                    if (legacyPacks.Count > 0 && !string.IsNullOrEmpty(saveMigratedPath))
                    {
                        // Автоматически обновляем файл на диске в новый читабельный формат
                        SavePacks(legacyPacks, saveMigratedPath);
                    }
                    return legacyPacks;
                }

                var packs = JsonSerializer.Deserialize<List<CommandPack>>(json, JsonOptions) ?? [];
                NormalizePacks(packs);
                return packs;
            }

            // 2. Корневой элемент - JSON-объект
            if (root.ValueKind == JsonValueKind.Object)
            {
                // Формат обертки: {"packs": [...]}
                if (root.TryGetProperty("packs", out var packsElem) && packsElem.ValueKind == JsonValueKind.Array)
                {
                    var packs = JsonSerializer.Deserialize<List<CommandPack>>(packsElem.GetRawText(), JsonOptions) ?? [];
                    NormalizePacks(packs);
                    return packs;
                }

                // Старый Save.json Laitis (имеет секции "D" или "B")
                if (root.TryGetProperty("D", out _) || root.TryGetProperty("B", out _))
                {
                    return LaitisImporter.ImportFromJson(json);
                }

                // Одиночный пакет: {"name": "...", "groups": [...]}
                var singlePack = JsonSerializer.Deserialize<CommandPack>(json, JsonOptions);
                if (singlePack != null)
                {
                    NormalizePack(singlePack);
                    return [singlePack];
                }
            }
        }
        catch
        {
            // Резервный парсинг через импортер Laitis
            try
            {
                return LaitisImporter.ImportFromJson(json);
            }
            catch
            {
                return [];
            }
        }

        return [];
    }

    private static bool IsLegacyLaitisFormat(JsonElement arrayElem)
    {
        foreach (var item in arrayElem.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                // В старом Laitis присутствуют однобуквенные свойства "N" или "P" без "name"
                if ((item.TryGetProperty("N", out _) || item.TryGetProperty("P", out _)) && !item.TryGetProperty("name", out _))
                    return true;
            }
        }
        return false;
    }

    private static void NormalizePacks(List<CommandPack> packs)
    {
        foreach (var pack in packs)
        {
            NormalizePack(pack);
        }
    }

    private static void NormalizePack(CommandPack pack)
    {
        if (string.IsNullOrEmpty(pack.Id))
            pack.Id = Guid.NewGuid().ToString("N");

        foreach (var group in pack.Groups)
        {
            if (string.IsNullOrEmpty(group.Id))
                group.Id = Guid.NewGuid().ToString("N");

            foreach (var cmd in group.Commands)
            {
                if (string.IsNullOrEmpty(cmd.Id))
                    cmd.Id = Guid.NewGuid().ToString("N");

                // Если имя команды не указано, используем первую фразу активации
                if (string.IsNullOrWhiteSpace(cmd.Name) && cmd.Phrases.Count > 0)
                {
                    cmd.Name = cmd.Phrases[0];
                }
            }
        }
    }

    /// <summary>
    /// Возвращает образцовый демонстрационный набор пакетов ReLaitis,
    /// охватывающий все возможности программы (кроме расширения браузера).
    /// </summary>
    public static List<CommandPack> GetDefaultShowcasePacks()
    {
        return
        [
            new CommandPack
            {
                Name = "Система и Управление Windows",
                ProcessFilter = "",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Окна и рабочий стол",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Свернуть все окна",
                                Phrases = ["сверни все окна", "покажи рабочий стол", "свернуть все"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Win+D"), new CommandAction(ActionType.Say, "Рабочий стол открыт")]
                            },
                            new VoiceCommand
                            {
                                Name = "Развернуть окно",
                                Phrases = ["разверни окно", "окно на весь экран", "максимизируй"],
                                Actions = [new CommandAction(ActionType.ShowWindow, "", "2")]
                            },
                            new VoiceCommand
                            {
                                Name = "Восстановить окно",
                                Phrases = ["восстанови окно", "окно в окно"],
                                Actions = [new CommandAction(ActionType.ShowWindow, "", "3")]
                            },
                            new VoiceCommand
                            {
                                Name = "Свернуть окно",
                                Phrases = ["сверни окно", "спрячь окно"],
                                Actions = [new CommandAction(ActionType.ShowWindow, "", "1")]
                            },
                            new VoiceCommand
                            {
                                Name = "Закрыть окно",
                                Phrases = ["закрой окно", "закрой это"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Alt+F4")]
                            }
                        ]
                    },
                    new CommandGroup
                    {
                        Name = "Системные утилиты",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Диспетчер задач",
                                Phrases = ["диспетчер задач", "открой диспетчер задач", "процессы"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Ctrl+Shift+Esc")]
                            },
                            new VoiceCommand
                            {
                                Name = "Снимок экрана",
                                Phrases = ["сделай скриншот", "снимок экрана", "ножницы"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Win+Shift+S")]
                            },
                            new VoiceCommand
                            {
                                Name = "Открыть блокнот",
                                Phrases = ["открой блокнот", "запусти блокнот", "блокнот"],
                                Actions = [new CommandAction(ActionType.OpenFile, "notepad.exe"), new CommandAction(ActionType.Say, "Блокнот открыт")]
                            },
                            new VoiceCommand
                            {
                                Name = "Закрыть блокнот",
                                Phrases = ["закрой блокнот", "убей блокнот"],
                                Actions = [new CommandAction(ActionType.CloseApp, "notepad", "0"), new CommandAction(ActionType.Say, "Блокнот закрыт")]
                            },
                            new VoiceCommand
                            {
                                Name = "Открыть калькулятор",
                                Phrases = ["открой калькулятор", "запусти калькулятор", "калькулятор"],
                                Actions = [new CommandAction(ActionType.OpenFile, "calc.exe")]
                            },
                            new VoiceCommand
                            {
                                Name = "Очистить корзину",
                                Phrases = ["очисти корзину", "очистить корзину"],
                                Actions =
                                [
                                    new CommandAction(ActionType.BatchScript, "PowerShell.exe -NoProfile -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\""),
                                    new CommandAction(ActionType.Say, "Корзина очищена")
                                ]
                            }
                        ]
                    },
                    new CommandGroup
                    {
                        Name = "Текст, буфер и медиа",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Вставить дату и время",
                                Phrases = ["вставь дату", "напечатай время", "какое сегодня число"],
                                Actions = [new CommandAction(ActionType.TypeText, "Сегодня {date}, текущее время {time}.", "0")]
                            },
                            new VoiceCommand
                            {
                                Name = "Озвучить буфер обмена",
                                Phrases = ["прочитай буфер", "что скопировано", "озвучь буфер"],
                                Actions = [new CommandAction(ActionType.Say, "В буфере обмена: {clipboard}")]
                            },
                            new VoiceCommand
                            {
                                Name = "Системное уведомление и звук",
                                Phrases = ["покажи уведомление", "тест уведомления"],
                                Actions =
                                [
                                    new CommandAction(ActionType.Notify, "0", "ReLaitis: Тестовое уведомление системы успешно доставлено!", "4000"),
                                    new CommandAction(ActionType.PlayAudio, @"C:\Windows\Media\notify.wav")
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Поиск в интернете",
                                Phrases = ["найди {query}", "поиск в яндексе {query}", "поищи {query}"],
                                Actions = [new CommandAction(ActionType.OpenURL, "https://ya.ru/search/?text={query}"), new CommandAction(ActionType.Say, "Ищу в интернете: {query}")]
                            }
                        ]
                    }
                ]
            },
            new CommandPack
            {
                Name = "Навигация, Мышь и JetAim",
                ProcessFilter = "",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Клики и скролл",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Левый клик",
                                Phrases = ["клик", "нажми", "левый клик"],
                                Actions = [new CommandAction(ActionType.MouseButton, "0", "0")]
                            },
                            new VoiceCommand
                            {
                                Name = "Правый клик",
                                Phrases = ["правый клик", "контекстное меню"],
                                Actions = [new CommandAction(ActionType.MouseButton, "2", "0")]
                            },
                            new VoiceCommand
                            {
                                Name = "Двойной клик",
                                Phrases = ["двойной клик", "два клика"],
                                Actions = [new CommandAction(ActionType.MouseButton, "0", "0"), new CommandAction(ActionType.Pause, "60"), new CommandAction(ActionType.MouseButton, "0", "0")]
                            },
                            new VoiceCommand
                            {
                                Name = "Прокрутка вниз",
                                Phrases = ["прокрути вниз", "скролл вниз", "ниже"],
                                Actions = [new CommandAction(ActionType.MouseScroll, "0", "-360")]
                            },
                            new VoiceCommand
                            {
                                Name = "Прокрутка вверх",
                                Phrases = ["прокрути вверх", "скролл вверх", "выше"],
                                Actions = [new CommandAction(ActionType.MouseScroll, "0", "360")]
                            },
                            new VoiceCommand
                            {
                                Name = "Курсор в центр экрана",
                                Phrases = ["мышь в центр", "курсор по центру"],
                                Actions = [new CommandAction(ActionType.MouseMove, "50", "50", "0", "1")]
                            },
                            new VoiceCommand
                            {
                                Name = "Сетка JetAim",
                                Phrases = ["сетка", "мышь сетка", "джетайм", "курсор сетка"],
                                Actions = [new CommandAction(ActionType.JetAim, "5")]
                            },
                            new VoiceCommand
                            {
                                Name = "Кликнуть по кнопке Пуск через UI Automation",
                                Phrases = ["нажми пуск", "кликни пуск", "кнопка пуск"],
                                Actions = [new CommandAction(ActionType.MouseMoveOn, "0", "Пуск")]
                            }
                        ]
                    }
                ]
            },
            new CommandPack
            {
                Name = "Умный Ассистент и Логика",
                ProcessFilter = "",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Диалоги и вычисления",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Приветствие со случайным ответом",
                                Phrases = ["привет", "здравствуй", "добрый день", "хай"],
                                Actions =
                                [
                                    new CommandAction(ActionType.RandomActionBlock),
                                    new CommandAction(ActionType.Say, "Здравствуйте! Чем могу помочь?"),
                                    new CommandAction(ActionType.Say, "Приветствую! ReLaitis готов к выполнению задач."),
                                    new CommandAction(ActionType.Say, "Рад вас слышать! Назовите команду."),
                                    new CommandAction(ActionType.EndBlock)
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Математический калькулятор",
                                Phrases = ["посчитай {x} плюс {y}", "сложи {x} и {y}"],
                                Actions =
                                [
                                    new CommandAction(ActionType.SetVariableValue, "sum", "{x} + {y}"),
                                    new CommandAction(ActionType.Say, "Результат сложения: {sum}")
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Генератор случайного числа",
                                Phrases = ["случайное число", "брось кубик", "назови число"],
                                Actions =
                                [
                                    new CommandAction(ActionType.SetVariableValue, "randVal", "{rnd:1:100}"),
                                    new CommandAction(ActionType.Say, "Выпало число {randVal}")
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Интерактивный диалог (Как дела)",
                                Phrases = ["как дела", "как настроение", "как поживаешь"],
                                Actions =
                                [
                                    new CommandAction(ActionType.Say, "Отлично! А у вас как дела?"),
                                    new CommandAction(ActionType.WaitNextPhrase, "userMood", "5000"),
                                    new CommandAction(ActionType.IfVariableValue, "userMood", "4", "хорош"),
                                    new CommandAction(ActionType.Say, "Очень рад за вас! Пусть весь день будет отличным."),
                                    new CommandAction(ActionType.Else),
                                    new CommandAction(ActionType.Say, "Понял вас. Надеюсь, я смогу помочь сделать этот день лучше!"),
                                    new CommandAction(ActionType.EndBlock)
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Проверка запущенности Блокнота",
                                Phrases = ["проверь блокнот", "запущен ли блокнот"],
                                Actions =
                                [
                                    new CommandAction(ActionType.IfProcessExists, "notepad"),
                                    new CommandAction(ActionType.Say, "Блокнот сейчас запущен в системе."),
                                    new CommandAction(ActionType.Else),
                                    new CommandAction(ActionType.Say, "Блокнот сейчас не запущен."),
                                    new CommandAction(ActionType.EndBlock)
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Цикл: обратный отсчет",
                                Phrases = ["обратный отсчет", "сделай отсчет", "отсчет"],
                                Actions =
                                [
                                    new CommandAction(ActionType.Say, "Начинаю отсчет"),
                                    new CommandAction(ActionType.SetVariableValue, "c", "3"),
                                    new CommandAction(ActionType.Loop, "3"),
                                    new CommandAction(ActionType.Say, "{c}"),
                                    new CommandAction(ActionType.Pause, "300"),
                                    new CommandAction(ActionType.SetVariableValue, "c", "{c} - 1"),
                                    new CommandAction(ActionType.EndBlock),
                                    new CommandAction(ActionType.Say, "Поехали!")
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Узнать внешний IP через веб-запрос",
                                Phrases = ["какой мой айпи", "внешний айпи", "узнай ip"],
                                Actions =
                                [
                                    new CommandAction(ActionType.HttpWebRequest, "0", "https://api.ipify.org", "myIp"),
                                    new CommandAction(ActionType.Say, "Ваш внешний IP адрес: {myIp}")
                                ]
                            },
                            new VoiceCommand
                            {
                                Name = "Включить рабочий профиль Блокнота",
                                Phrases = ["включи рабочий профиль", "активируй профиль блокнота"],
                                Actions =
                                [
                                    new CommandAction(ActionType.TogglePackActivity, "Профиль: Блокнот", "1"),
                                    new CommandAction(ActionType.Say, "Рабочий профиль активирован")
                                ]
                            }
                        ]
                    }
                ]
            },
            new CommandPack
            {
                Name = "Профиль: Блокнот",
                ProcessFilter = "notepad.exe",
                Groups =
                [
                    new CommandGroup
                    {
                        Name = "Команды редактирования текста",
                        Commands =
                        [
                            new VoiceCommand
                            {
                                Name = "Выделить всё",
                                Phrases = ["выдели все", "выделить все", "выделить весь текст"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Ctrl+A")]
                            },
                            new VoiceCommand
                            {
                                Name = "Скопировать текст",
                                Phrases = ["скопируй", "копировать"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Ctrl+C"), new CommandAction(ActionType.Say, "Скопировано")]
                            },
                            new VoiceCommand
                            {
                                Name = "Вставить текст",
                                Phrases = ["вставь", "вставить"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Ctrl+V")]
                            },
                            new VoiceCommand
                            {
                                Name = "Сохранить файл",
                                Phrases = ["сохрани", "сохрани файл", "сохранить документ"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Ctrl+S"), new CommandAction(ActionType.Say, "Сохраняю файл")]
                            },
                            new VoiceCommand
                            {
                                Name = "Новая строка",
                                Phrases = ["новая строка", "перенос строки", "абзац"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Enter")]
                            },
                            new VoiceCommand
                            {
                                Name = "Отменить действие",
                                Phrases = ["отмени", "шаг назад", "отмена действия"],
                                Actions = [new CommandAction(ActionType.Hotkeys, "Ctrl+Z")]
                            },
                            new VoiceCommand
                            {
                                Name = "Вставить шаблон заметки",
                                Phrases = ["вставь шаблон", "заполни документ", "шаблон заметки"],
                                Actions =
                                [
                                    new CommandAction(ActionType.TypeText, "=== ЗАМЕТКА RELAITIS ===\nДата: {date} {time}\nСтатус: Все системы работают в штатном режиме!\n-------------------------\n", "0"),
                                    new CommandAction(ActionType.Say, "Шаблон заметки добавлен")
                                ]
                            }
                        ]
                    }
                ]
            }
        ];
    }
}
