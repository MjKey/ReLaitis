using System.Text.Json;
using System.Text.Json.Serialization;
using ReLaitis.Core.Enums;

namespace ReLaitis.Core.Helpers;

/// <summary>
/// Конвертер для ActionType enum:
/// Записывает понятные имена действий строками ("OpenFile", "Hotkeys", "Say"),
/// а при чтении поддерживает регистронезависимые строки ("hotkeys", "Say") и числовые ID (0..41).
/// </summary>
public class ActionTypeJsonConverter : JsonConverter<ActionType>
{
    public override ActionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var intVal) && Enum.IsDefined(typeof(ActionType), intVal))
                return (ActionType)intVal;
            return ActionType.Comment;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (!string.IsNullOrWhiteSpace(str))
            {
                if (Enum.TryParse<ActionType>(str, ignoreCase: true, out var result))
                    return result;

                if (int.TryParse(str, out var parsedInt) && Enum.IsDefined(typeof(ActionType), parsedInt))
                    return (ActionType)parsedInt;
            }
        }

        return ActionType.Comment;
    }

    public override void Write(Utf8JsonWriter writer, ActionType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Конвертер параметров действия string[]:
/// Позволяет передавать как массив ["calc.exe"], так и одиночную строку "calc.exe" или число 500.
/// Это защищает от ошибок парсинга, если LLM или человек опустил квадратные скобки.
/// </summary>
public class StringArrayOrSingleJsonConverter : JsonConverter<string[]>
{
    public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return [];

        if (reader.TokenType == JsonTokenType.String)
        {
            var val = reader.GetString();
            return string.IsNullOrEmpty(val) ? [] : [val];
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return [reader.GetInt64().ToString()];
        }

        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
        {
            return [reader.GetBoolean() ? "true" : "false"];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        list.Add(reader.GetString() ?? "");
                        break;
                    case JsonTokenType.Number:
                        list.Add(reader.GetInt64().ToString());
                        break;
                    case JsonTokenType.True:
                        list.Add("true");
                        break;
                    case JsonTokenType.False:
                        list.Add("false");
                        break;
                    case JsonTokenType.Null:
                        list.Add("");
                        break;
                }
            }
            return list.ToArray();
        }

        return [];
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value != null)
        {
            foreach (var item in value)
            {
                writer.WriteStringValue(item ?? "");
            }
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// Конвертер списка фраз List<string>:
/// Принимает как массив фраз ["фраза 1", "фраза 2"], так и одиночную строку "фраза".
/// </summary>
public class StringListOrSingleJsonConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return [];

        if (reader.TokenType == JsonTokenType.String)
        {
            var val = reader.GetString();
            return string.IsNullOrEmpty(val) ? [] : [val.Trim()];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var val = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        list.Add(val.Trim());
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    list.Add(reader.GetInt64().ToString());
                }
            }
            return list;
        }

        return [];
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value != null)
        {
            foreach (var item in value)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    writer.WriteStringValue(item);
            }
        }
        writer.WriteEndArray();
    }
}
