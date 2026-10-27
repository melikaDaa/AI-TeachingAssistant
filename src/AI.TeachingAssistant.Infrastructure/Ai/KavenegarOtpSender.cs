using System.Net;
using System.Text.Json;
using AI.TeachingAssistant.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI.TeachingAssistant.Infrastructure.Ai;

/// <summary>Kavenegar VerifyLookup adapter. Secrets and OTP values are never logged.</summary>
public sealed class KavenegarOtpSender : IPhoneOtpSender
{
    private readonly HttpClient _http;
    private readonly KavenegarOptions _options;
    private readonly ILogger<KavenegarOtpSender> _logger;

    public KavenegarOtpSender(HttpClient http, IOptions<KavenegarOptions> options, ILogger<KavenegarOtpSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.OtpTemplate) ||
            _http.BaseAddress is null || !string.Equals(_http.BaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new KavenegarDeliveryException("Kavenegar configuration is incomplete or insecure.", false);

        var apiKey = Uri.EscapeDataString(_options.ApiKey);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiKey}/verify/lookup.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["receptor"] = phoneNumber,
                ["token"] = code,
                ["template"] = _options.OtpTemplate,
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Kavenegar OTP network error: {ErrorType}", ex.GetType().Name);
            throw new KavenegarDeliveryException("Kavenegar is temporarily unavailable.", true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Kavenegar OTP request timed out.");
            throw new KavenegarDeliveryException("Kavenegar request timed out.", true);
        }

        using (response)
        {
            var status = await TryReadServiceStatusAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode || status != 200)
            {
                var retryable = response.StatusCode == HttpStatusCode.RequestTimeout ||
                    (int)response.StatusCode == 429 || (int)response.StatusCode >= 500 || status is 409;
                _logger.LogWarning("Kavenegar rejected OTP delivery. HttpStatus={HttpStatus}, ServiceStatus={ServiceStatus}",
                    (int)response.StatusCode, status);
                throw new KavenegarDeliveryException("Kavenegar did not accept the OTP request.", retryable);
            }
        }
    }

    private async Task<int?> TryReadServiceStatusAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.TryGetProperty("return", out var result) &&
                   result.TryGetProperty("status", out var status) && status.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (JsonException) { return null; }
    }
}

public sealed class KavenegarDeliveryException(string message, bool isTransient) : Exception(message)
{
    public bool IsTransient { get; } = isTransient;
}
