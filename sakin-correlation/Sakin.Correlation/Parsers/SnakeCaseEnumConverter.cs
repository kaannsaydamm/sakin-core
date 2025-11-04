using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sakin.Correlation.Parsers;

internal sealed class SnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> NameToValue = BuildNameToValueMap();
    private static readonly Dictionary<TEnum, string> ValueToName = BuildValueToNameMap();

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string token when parsing {typeof(TEnum).Name}.");
        }

        var rawValue = reader.GetString() ?? string.Empty;
        var normalized = Normalize(rawValue);

        if (NameToValue.TryGetValue(normalized, out var enumValue))
        {
            return enumValue;
        }

        throw new JsonException($"Unable to convert '{rawValue}' to enum {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (!ValueToName.TryGetValue(value, out var name))
        {
            name = ConvertToSnakeCase(value.ToString());
        }

        writer.WriteStringValue(name);
    }

    private static Dictionary<string, TEnum> BuildNameToValueMap()
    {
        var map = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var enumValue = (TEnum)field.GetValue(null)!;
            var defaultName = field.Name;
            var snakeCaseName = ConvertToSnakeCase(defaultName);

            map[Normalize(defaultName)] = enumValue;
            map[Normalize(snakeCaseName)] = enumValue;
            map[Normalize(ConvertToKebabCase(defaultName))] = enumValue;

            var jsonAttribute = field.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (jsonAttribute is not null)
            {
                map[Normalize(jsonAttribute.Name)] = enumValue;
            }

            var enumMemberAttribute = field.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>();
            if (enumMemberAttribute is not null && !string.IsNullOrWhiteSpace(enumMemberAttribute.Value))
            {
                map[Normalize(enumMemberAttribute.Value!)] = enumValue;
            }
        }

        return map;
    }

    private static Dictionary<TEnum, string> BuildValueToNameMap()
    {
        var map = new Dictionary<TEnum, string>();

        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var enumValue = (TEnum)field.GetValue(null)!;
            var jsonAttribute = field.GetCustomAttribute<JsonPropertyNameAttribute>();
            var enumMemberAttribute = field.GetCustomAttribute<System.Runtime.Serialization.EnumMemberAttribute>();

            if (jsonAttribute is not null)
            {
                map[enumValue] = jsonAttribute.Name;
            }
            else if (enumMemberAttribute is not null && !string.IsNullOrWhiteSpace(enumMemberAttribute.Value))
            {
                map[enumValue] = enumMemberAttribute.Value!;
            }
            else
            {
                map[enumValue] = ConvertToSnakeCase(field.Name);
            }
        }

        return map;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("-", "_")
            .Replace(" ", "_")
            .Trim();

        return normalized.ToLowerInvariant();
    }

    private static string ConvertToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length * 2);

        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static string ConvertToKebabCase(string value)
    {
        return ConvertToSnakeCase(value).Replace('_', '-');
    }
}
