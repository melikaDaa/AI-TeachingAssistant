using System.Text.Json;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Ai;

/// <summary>Extracts the JSON object from a raw AI response (tolerates fences and stray text).</summary>
public static class AiJson
{
    public static T? Parse<T>(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return default;

        var json = Extract(raw);
        if (json is null) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, PayloadJson.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Takes the text between the first '{' and its matching '}'.</summary>
    public static string? Extract(string raw)
    {
        var start = raw.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < raw.Length; i++)
        {
            var c = raw[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return raw[start..(i + 1)];
            }
        }
        return null;
    }
}
