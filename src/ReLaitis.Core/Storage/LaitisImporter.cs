using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ICSharpCode.SharpZipLib.Zip;
using ReLaitis.Core.Enums;
using ReLaitis.Core.Models;

namespace ReLaitis.Core.Storage;

/// <summary>
/// Импортер пакетов и макросов из оригинальных файлов конфигурации Laitis (.laitis и Save.json).
/// Полная обратная совместимость со всеми версиями Laitis.
/// </summary>
public static class LaitisImporter
{
    public const string DefaultLaitisPassword = "laitis";

    /// <summary>
    /// Загружает и преобразует пакеты команд из файла .laitis (архива) или .json.
    /// </summary>
    public static List<CommandPack> ImportFromFile(string filePath, string password = DefaultLaitisPassword)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Файл не найден: {filePath}");

        string jsonContent;

        // Если это ZIP-архив .laitis (начинается с 'PK')
        if (IsZipArchive(filePath))
        {
            jsonContent = ExtractSaveJsonFromZip(filePath, password);
        }
        else
        {
            jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
        }

        return ImportFromJson(jsonContent);
    }

    /// <summary>
    /// Парсит JSON-строку Laitis (формат Save.json) и возвращает список CommandPack.
    /// </summary>
    public static List<CommandPack> ImportFromJson(string jsonContent)
    {
        var result = new List<CommandPack>();

        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // В файлах Laitis список пакетов хранится в root["D"]["B"] (или в root["B"])
        JsonElement packsArrayElement = default;
        var found = false;

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("D", out var dElem) && dElem.ValueKind == JsonValueKind.Object)
            {
                if (dElem.TryGetProperty("B", out var bElem) && bElem.ValueKind == JsonValueKind.Array)
                {
                    packsArrayElement = bElem;
                    found = true;
                }
            }

            if (!found && root.TryGetProperty("B", out var directB) && directB.ValueKind == JsonValueKind.Array)
            {
                packsArrayElement = directB;
                found = true;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            packsArrayElement = root;
            found = true;
        }

        if (!found)
            return result;

        foreach (var packJson in packsArrayElement.EnumerateArray())
        {
            var pack = ParsePack(packJson);
            if (pack != null)
            {
                result.Add(pack);
            }
        }

        return result;
    }

    private static CommandPack? ParsePack(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Object)
            return null;

        var pack = new CommandPack
        {
            Id = elem.TryGetProperty("ID", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
            Name = elem.TryGetProperty("N", out var n) ? n.GetString() ?? "Без названия" : "Без названия",
            Culture = elem.TryGetProperty("C", out var c) ? c.GetString() ?? "ru-RU" : "ru-RU",
            IsActive = !elem.TryGetProperty("A", out var a) || a.GetBoolean(),
            Version = elem.TryGetProperty("V", out var v) && v.TryGetInt32(out var vInt) ? vInt : 1,
            Order = elem.TryGetProperty("O", out var o) && o.TryGetInt32(out var oInt) ? oInt : 0
        };

        // Извлекаем переменные пакета ("W": [{"K": "имя", "V": "значение"}])
        if (elem.TryGetProperty("W", out var wElem) && wElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var varItem in wElem.EnumerateArray())
            {
                if (varItem.TryGetProperty("K", out var k) && varItem.TryGetProperty("V", out var val))
                {
                    var kStr = k.GetString();
                    if (!string.IsNullOrEmpty(kStr))
                        pack.Variables[kStr] = val.GetString() ?? "";
                }
            }
        }

        // Извлекаем правила автозамены ("Y": [ {"ID": "...", "W": "...", "R": "..."} ])
        if (elem.TryGetProperty("Y", out var yElem) && yElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var ruleItem in yElem.EnumerateArray())
            {
                if (ruleItem.TryGetProperty("W", out var w) && ruleItem.TryGetProperty("R", out var r))
                {
                    var rule = new Rule
                    {
                        Id = ruleItem.TryGetProperty("ID", out var rId) ? rId.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
                        Word = w.GetString() ?? "",
                        Replacement = r.GetString() ?? ""
                    };
                    pack.Rules.Add(rule);
                }
            }
        }

        // Извлекаем категории ("P": [ ... ])
        if (elem.TryGetProperty("P", out var pElem) && pElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var groupJson in pElem.EnumerateArray())
            {
                var group = ParseGroup(groupJson);
                if (group != null)
                {
                    pack.Groups.Add(group);
                }
            }
        }

        DetectProcessFilter(pack);

        return pack;
    }

    private static CommandGroup? ParseGroup(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Object)
            return null;

        var group = new CommandGroup
        {
            Id = elem.TryGetProperty("ID", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
            Name = elem.TryGetProperty("N", out var n) ? n.GetString() ?? "Группа" : "Группа",
            IsEnabled = !elem.TryGetProperty("E", out var e) || e.GetBoolean(),
            Order = elem.TryGetProperty("O", out var o) && o.TryGetInt32(out var oInt) ? oInt : 0
        };

        // Извлекаем команды внутри категории ("C": [ ... ])
        if (elem.TryGetProperty("C", out var cElem) && cElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var cmdJson in cElem.EnumerateArray())
            {
                var cmd = ParseCommand(cmdJson);
                if (cmd != null)
                {
                    group.Commands.Add(cmd);
                }
            }
        }

        return group;
    }

    private static VoiceCommand? ParseCommand(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Object)
            return null;

        var cmd = new VoiceCommand
        {
            Id = elem.TryGetProperty("ID", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
            Name = elem.TryGetProperty("N", out var n) ? n.GetString() ?? string.Empty : string.Empty,
            IsEnabled = !elem.TryGetProperty("E", out var e) || e.GetBoolean(),
            Order = elem.TryGetProperty("O", out var o) && o.TryGetInt32(out var oInt) ? oInt : 0
        };

        // Извлекаем фразы активации ("V": ["фраза 1", "фраза 2"])
        if (elem.TryGetProperty("V", out var vElem) && vElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var phrase in vElem.EnumerateArray())
            {
                var str = phrase.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    cmd.Phrases.Add(str.Trim());
                }
            }
        }

        // Если у команды не было задано имя, генерируем его из первой фразы
        if (string.IsNullOrEmpty(cmd.Name) && cmd.Phrases.Count > 0)
        {
            cmd.Name = cmd.Phrases[0];
        }

        // Извлекаем действия ("A": [{"T": 0, "P": ["calc.exe"]}])
        if (elem.TryGetProperty("A", out var aElem) && aElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var actJson in aElem.EnumerateArray())
            {
                var action = ParseAction(actJson);
                if (action != null)
                {
                    cmd.Actions.Add(action);
                }
            }
        }

        return cmd;
    }

    private static CommandAction? ParseAction(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Object)
            return null;

        if (!elem.TryGetProperty("T", out var tElem) || !tElem.TryGetInt32(out var tInt))
            return null;

        var type = (ActionType)tInt;
        var parameters = new List<string>();

        if (elem.TryGetProperty("P", out var pElem) && pElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in pElem.EnumerateArray())
            {
                parameters.Add(param.GetString() ?? string.Empty);
            }
        }

        return new CommandAction(type, parameters.ToArray());
    }

    private static void DetectProcessFilter(CommandPack pack)
    {
        // В Laitis специализированные игровые пакеты (например Overwatch, Dota) ставили IfProcessSelected в каждую команду
        // Проверяем, имеют ли ВСЕ команды пакета один и тот же фильтр процессов
        var allCommands = pack.Groups.SelectMany(g => g.Commands).ToList();
        if (allCommands.Count == 0)
            return;

        string? commonFilter = null;

        foreach (var c in allCommands)
        {
            // Проверяем первое предусловие
            var firstIf = c.Actions.FirstOrDefault(a => a.Type == ActionType.IfProcessSelected);
            if (firstIf == null || firstIf.Parameters.Length == 0 || string.IsNullOrWhiteSpace(firstIf.Parameters[0]))
            {
                // Есть команды без фильтра процессов - пакет не должен блокироваться целиком
                return;
            }

            var filter = firstIf.Parameters[0];
            if (commonFilter == null)
            {
                commonFilter = filter;
            }
            else if (!string.Equals(commonFilter, filter, StringComparison.OrdinalIgnoreCase))
            {
                // Разные команды пакета фильтруют разные процессы
                return;
            }
        }

        if (!string.IsNullOrEmpty(commonFilter))
        {
            pack.ProcessFilter = commonFilter;
        }
    }

    private static bool IsZipArchive(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var b1 = stream.ReadByte();
        var b2 = stream.ReadByte();
        return b1 == 'P' && b2 == 'K';
    }

    private static string ExtractSaveJsonFromZip(string zipFilePath, string password)
    {
        using var stream = File.OpenRead(zipFilePath);
        using var zipFile = new ZipFile(stream);

        if (!string.IsNullOrEmpty(password))
            zipFile.Password = password;

        foreach (ZipEntry entry in zipFile)
        {
            if (entry.IsFile && entry.Name.EndsWith("Save.json", StringComparison.OrdinalIgnoreCase))
            {
                using var entryStream = zipFile.GetInputStream(entry);
                using var reader = new StreamReader(entryStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }

        throw new InvalidOperationException("Внутри архива .laitis не найден файл Save.json");
    }
}
