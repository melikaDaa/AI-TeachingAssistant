using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AI.TeachingAssistant.Application.Common;
using Microsoft.Extensions.Logging;

namespace AI.TeachingAssistant.Infrastructure.Ai;

/// <summary>Configuration for real image rendering. Comes only from configuration/env vars.</summary>
public sealed class ImageGenerationOptions
{
    public const string SectionName = "ImageGeneration";

    /// <summary>"xkiro" enables real rendering; anything else (default) leaves it off.</summary>
    public string Provider { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.xkiro.com/v1";

    /// <summary>Free tier model by default; the paid quality model is opt-in.</summary>
    public string Model { get; set; } = "sensenova/sensenova-u1.5-lite";

    public string Size { get; set; } = "1024x1024";

    /// <summary>Total budget for one image job (submit + poll + download). Default 5 minutes.</summary>
    public int PollTimeoutSeconds { get; set; } = 300;

    /// <summary>Cost guard: at most this many images are rendered per section.</summary>
    public int MaxImagesPerSection { get; set; } = 2;

    /// <summary>Folder for downloaded images; empty ⇒ default under the app's App_Data.</summary>
    public string StorageRoot { get; set; } = string.Empty;

    public bool IsEnabled =>
        Provider.Equals("xkiro", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// xKiro image generation over the asynchronous job API (submit → poll → download). Mirrors the
/// text provider's safety rules: the key never leaves the backend and is never logged, and only
/// teacher-safe Persian messages surface.
/// </summary>
public class XKiroImageProvider : IImageProvider
{
    private readonly HttpClient _http;
    private readonly ImageGenerationOptions _options;
    private readonly ILogger<XKiroImageProvider>? _logger;

    public XKiroImageProvider(HttpClient http, ImageGenerationOptions options, ILogger<XKiroImageProvider>? logger = null)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string Name => $"xKiro Image ({_options.Model})";

    public bool IsEnabled => _options.IsEnabled;

    public int MaxImagesPerSection => Math.Max(0, _options.MaxImagesPerSection);

    public async Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new AiProviderException("کلید دسترسی سرویس تولید تصویر تنظیم نشده است.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.PollTimeoutSeconds)));

        _logger?.LogInformation(
            "Image request via {Model}: promptChars={Chars} size={Size}",
            _options.Model, request.Prompt.Length, _options.Size);

        try
        {
            var jobId = await CreateJobAsync(request.Prompt, timeoutCts.Token);
            var url = await PollUntilReadyAsync(jobId, timeoutCts.Token);
            return await DownloadAsync(url, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // An image deadline is a provider failure, not cancellation of the lesson worker.
            throw new AiProviderException("تولید تصویر بیش از حد طول کشید. لطفاً دوباره تلاش کنید.", isTransient: true);
        }
    }

    private async Task<string> CreateJobAsync(string prompt, CancellationToken ct)
    {
        var body = new CreateJobRequest(_options.Model, prompt, N: 1, Size: _options.Size);
        using var http = new HttpRequestMessage(HttpMethod.Post, "images/generations");
        http.Content = JsonContent.Create(body);
        // Per-request auth header: never mutate DefaultRequestHeaders on a pooled typed client.
        http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(http, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException("پاسخ سرویس تولید تصویر بیش از حد طول کشید.", isTransient: true);
        }
        catch (HttpRequestException)
        {
            throw new AiProviderException("ارتباط با سرویس تولید تصویر برقرار نشد. لطفاً دوباره تلاش کنید.", isTransient: true);
        }

        using var responseScope = response;
        if ((int)response.StatusCode is 401 or 403)
            throw new AiProviderException("احراز هویت سرویس تولید تصویر ناموفق بود.");
        if ((int)response.StatusCode == 429)
            throw new AiProviderException("تعداد درخواست‌های تولید تصویر بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.", isTransient: true);
        if (!response.IsSuccessStatusCode)
        {
            await LogErrorBodyAsync(response, ct);
            throw new AiProviderException("سرویس تولید تصویر در پاسخ‌دهی به مشکل خورد. لطفاً دوباره تلاش کنید.", isTransient: true);
        }

        CreateJobResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<CreateJobResponse>(cancellationToken: ct);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new AiProviderException("پاسخ سرویس تولید تصویر با ساختار مورد انتظار سازگار نبود.");
        }

        if (string.IsNullOrWhiteSpace(payload?.Id))
            throw new AiProviderException("پاسخ سرویس تولید تصویر با ساختار مورد انتظار سازگار نبود.");

        return payload.Id;
    }

    private async Task<string> PollUntilReadyAsync(string jobId, CancellationToken ct)
    {
        // Poll every few seconds with backoff — aggressive polling counts against the rate limit.
        var wait = TimeSpan.FromSeconds(2);
        while (true)
        {
            try
            {
                await Task.Delay(wait, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AiProviderException("تولید تصویر بیش از حد طول کشید. لطفاً دوباره تلاش کنید.", isTransient: true);
            }

            JobResponse? job;
            try
            {
                using var response = await GetWithRetryAsync(new Uri($"images/generations/{Uri.EscapeDataString(jobId)}", UriKind.Relative), authenticated: true, ct);
                if ((int)response.StatusCode is 401 or 403)
                    throw new AiProviderException("احراز هویت سرویس تولید تصویر ناموفق بود.");
                if (!response.IsSuccessStatusCode)
                {
                    await LogErrorBodyAsync(response, ct);
                    throw new AiProviderException("دریافت وضعیت تولید تصویر ناموفق بود. لطفاً دوباره تلاش کنید.", isTransient: true);
                }
                job = await response.Content.ReadFromJsonAsync<JobResponse>(cancellationToken: ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AiProviderException("تولید تصویر بیش از حد طول کشید. لطفاً دوباره تلاش کنید.", isTransient: true);
            }
            catch (HttpRequestException)
            {
                throw new AiProviderException("ارتباط با سرویس تولید تصویر برقرار نشد. لطفاً دوباره تلاش کنید.", isTransient: true);
            }

            var status = job?.Status?.ToLowerInvariant();
            switch (status)
            {
                case "succeeded":
                    var url = job?.Data?.FirstOrDefault()?.Url;
                    if (string.IsNullOrWhiteSpace(url))
                        throw new AiProviderException("نشانی تصویر تولیدشده دریافت نشد.");
                    return url;

                case "failed":
                    _logger?.LogWarning("Image job {JobId} failed: {Message}", jobId, job?.Error?.Message);
                    throw new AiProviderException("تولید تصویر ناموفق بود. لطفاً دوباره تلاش کنید.", isTransient: true);

                case "blocked":
                    _logger?.LogWarning("Image job {JobId} blocked on content grounds", jobId);
                    throw new AiProviderException("پرامپت تصویر از نظر محتوایی پذیرفته نشد؛ لطفاً آن را با کلمات دیگری بنویسید.");

                case "processing":
                case "pending":
                case null:
                    wait = TimeSpan.FromMilliseconds(Math.Min(wait.TotalMilliseconds * 1.5, 10_000));
                    continue;

                default:
                    _logger?.LogWarning("Image job {JobId} reported unknown status {Status}", jobId, status);
                    wait = TimeSpan.FromMilliseconds(Math.Min(wait.TotalMilliseconds * 1.5, 10_000));
                    continue;
            }
        }
    }

    private async Task<GeneratedImage> DownloadAsync(string url, CancellationToken ct)
    {
        var absolute = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
        if (absolute is null)
            throw new AiProviderException("نشانی تصویر تولیدشده نامعتبر بود.");

        HttpResponseMessage response;
        try
        {
            response = await GetWithRetryAsync(absolute, authenticated: false, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException("دریافت تصویر تولیدشده بیش از حد طول کشید.", isTransient: true);
        }
        catch (HttpRequestException)
        {
            throw new AiProviderException("دریافت تصویر تولیدشده با مشکل روبه‌رو شد. لطفاً دوباره تلاش کنید.", isTransient: true);
        }

        using var responseScope = response;
        if (!response.IsSuccessStatusCode)
        {
            await LogErrorBodyAsync(response, ct);
            throw new AiProviderException("دریافت تصویر تولیدشده با مشکل روبه‌رو شد. لطفاً دوباره تلاش کنید.", isTransient: true);
        }

        var data = await response.Content.ReadAsByteArrayAsync(ct);
        if (data.Length == 0)
            throw new AiProviderException("تصویر تولیدشده خالی بود.");

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            contentType = "image/png";

        _logger?.LogInformation("Image downloaded: bytes={Bytes} contentType={ContentType}", data.Length, contentType);
        return new GeneratedImage(data, contentType);
    }

    // Retry only idempotent GETs. Retrying submission could create duplicate billable jobs.
    private async Task<HttpResponseMessage> GetWithRetryAsync(Uri uri, bool authenticated, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            TimeSpan? retryAfter = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (authenticated)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                var response = await _http.SendAsync(request, ct);
                var transient = (int)response.StatusCode is 408 or 429 or >= 500;
                if (!transient || attempt >= 2) return response;
                retryAfter = response.Headers.RetryAfter?.Delta;
                if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
                    retryAfter = date - DateTimeOffset.UtcNow;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < 2 && !ct.IsCancellationRequested) { }
            catch (OperationCanceledException) when (attempt < 2 && !ct.IsCancellationRequested) { }

            var delay = retryAfter ?? TimeSpan.FromSeconds(attempt + 1);
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            _logger?.LogWarning("Temporary image GET failure; retry {Attempt} of 2", attempt + 1);
            await Task.Delay(delay, ct);
        }
    }

    private Task LogErrorBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        _logger?.LogError(
            "Image provider returned {Status}", (int)response.StatusCode);
        return Task.CompletedTask;
    }

    private sealed record CreateJobRequest(string Model, string Prompt, int N, string Size);

    private sealed class CreateJobResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
    }

    private sealed class JobResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("data")] public List<JobImage>? Data { get; set; }
        [JsonPropertyName("error")] public JobError? Error { get; set; }
    }

    private sealed class JobImage
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    private sealed class JobError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
