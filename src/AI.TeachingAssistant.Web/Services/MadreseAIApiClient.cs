using System.Net.Http.Json;
using AI.TeachingAssistant.Application.Dtos;

namespace AI.TeachingAssistant.Web.Services;

/// <summary>Surfaced API failures with the server's friendly Persian message.</summary>
public class MadreseAIApiException : Exception
{
    public MadreseAIApiException(string message) : base(message) { }
}

/// <summary>Typed client over the REST API. Keeps HTTP details out of the components.</summary>
public class MadreseAIApiClient
{
    private readonly HttpClient _http;

    public MadreseAIApiClient(HttpClient http) => _http = http;

    public async Task LogoutAsync()
    {
        var csrf = await _http.GetFromJsonAsync<CsrfResponse>("api/account/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/account/logout");
        if (!string.IsNullOrWhiteSpace(csrf?.Token)) request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new MadreseAIApiException("Could not log out. Please try again.");
    }

    private sealed record CsrfResponse(string Token);

    // ---------- teacher ----------
    public Task<TeacherDto?> GetTeacherAsync() => Get<TeacherDto>("api/teacher");
    public Task<UsageDto?> GetUsageAsync() => Get<UsageDto>("api/usage");
    public Task<TeacherDto?> UpdateTeacherAsync(UpdateTeacherRequest request) => Send<UpdateTeacherRequest, TeacherDto>(HttpMethod.Put, "api/teacher", request);

    // ---------- lessons ----------
    public Task<List<LessonSummaryDto>?> GetLessonsAsync() => Get<List<LessonSummaryDto>>("api/lessons");
    public Task<LessonSummaryDto?> CreateLessonAsync(CreateLessonRequest request) => Send<CreateLessonRequest, LessonSummaryDto>(HttpMethod.Post, "api/lessons", request);
    public Task<TeachingPackageDto?> GetLessonAsync(int id) => Get<TeachingPackageDto>($"api/lessons/{id}");
    public Task DeleteLessonAsync(int id) => ExpectSuccess(HttpMethod.Delete, $"api/lessons/{id}");

    // ---------- generation ----------
    public Task StartGenerationAsync(int id) => ExpectSuccess(HttpMethod.Post, $"api/lessons/{id}/generate");
    public Task StartSectionGenerationAsync(int id, string section) => ExpectSuccess(HttpMethod.Post, $"api/lessons/{id}/generate/{section}");
    public Task<TeachingPackageDto?> SaveSectionAsync(int id, string section, string payloadJson) =>
        Send<SectionEditRequest, TeachingPackageDto>(HttpMethod.Put, $"api/lessons/{id}/sections/{section}", new SectionEditRequest(payloadJson));

    // ---------- helpers ----------
    private async Task<T?> Get<T>(string url)
    {
        using var response = await _http.GetAsync(url);
        return await Read<T>(response);
    }

    private async Task<TRes?> Send<TReq, TRes>(HttpMethod method, string url, TReq body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        using var response = await _http.SendAsync(request);
        return await Read<TRes>(response);
    }

    private async Task ExpectSuccess(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new MadreseAIApiException(await ExtractError(response) ?? "در انجام درخواست مشکلی پیش آمد. لطفاً دوباره تلاش کنید.");
    }

    private static async Task<T?> Read<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>();

        throw new MadreseAIApiException(await ExtractError(response) ?? "در انجام درخواست مشکلی پیش آمد. لطفاً دوباره تلاش کنید.");
    }

    private static async Task<string?> ExtractError(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiError>();
            return payload?.Error;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ApiError
    {
        public string? Error { get; set; }
    }
}

public sealed class UsageDto
{
    public UsageBucketDto Text { get; set; } = new();
    public UsageBucketDto Image { get; set; } = new();
    public DateTime ResetsAtUtc { get; set; }
    public int WarningPercent { get; set; }
    public Dictionary<string, int> Operations { get; set; } = new();
}

public sealed class UsageBucketDto
{
    public int Limit { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }
}
