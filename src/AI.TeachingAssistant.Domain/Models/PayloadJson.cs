using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.TeachingAssistant.Domain.Models;

/// <summary>Shared JSON (de)serialization for section payloads. Enums bind to/from their names.</summary>
public static class PayloadJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T model) => JsonSerializer.Serialize(model, Options);

    public static T? Deserialize<T>(this string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
