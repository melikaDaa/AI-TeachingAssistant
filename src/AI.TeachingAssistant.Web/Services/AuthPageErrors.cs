using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.TeachingAssistant.Web.Services;

public static class AuthPageErrors
{
    public static string Read(string? body)
    {
        try
        {
            var result = JsonSerializer.Deserialize<ErrorResponse>(body ?? "", JsonOptions);
            return result?.Error ?? "درخواست انجام نشد. اطلاعات واردشده را بررسی کنید.";
        }
        catch { return "درخواست انجام نشد. اطلاعات واردشده را بررسی کنید."; }
    }

    public static async Task<string> ReadAsync(HttpResponseMessage response)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            return result?.Error ?? "درخواست انجام نشد. اطلاعات واردشده را بررسی کنید.";
        }
        catch { return "درخواست انجام نشد. اطلاعات واردشده را بررسی کنید."; }
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record ErrorResponse(string? Error);
}
