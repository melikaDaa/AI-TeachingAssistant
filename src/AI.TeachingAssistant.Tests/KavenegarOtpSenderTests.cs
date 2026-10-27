using System.Net;
using System.Text;
using AI.TeachingAssistant.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class KavenegarOtpSenderTests
{
    [Fact]
    public async Task Lookup_posts_receptor_token_and_exact_template_to_documented_endpoint()
    {
        var handler = new CaptureHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"return\":{\"status\":200,\"message\":\"تایید شد\"},\"entries\":[{}]}", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.kavenegar.com/v1/") };
        var sender = Create(http);
        await sender.SendAsync("+989123456789", "482193");

        Assert.Equal("POST", handler.Method);
        Assert.Equal("https://api.kavenegar.com/v1/encoded-api-key/verify/lookup.json", handler.Uri);
        Assert.Equal("application/x-www-form-urlencoded", handler.ContentType);
        Assert.Contains("receptor=%2B989123456789", handler.Body);
        Assert.Contains("token=482193", handler.Body);
        Assert.Contains("template=teacher-otp-approved", handler.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"return\":{\"status\":424,\"message\":\"template missing\"}}", false)]
    [InlineData(HttpStatusCode.Forbidden, "{\"return\":{\"status\":403,\"message\":\"invalid key\"}}", false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{\"return\":{\"status\":409}}", true)]
    public async Task Rejected_responses_are_not_reported_as_success(HttpStatusCode httpStatus, string body, bool transient)
    {
        var handler = new CaptureHandler(new HttpResponseMessage(httpStatus)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.kavenegar.com/v1/") };
        var error = await Assert.ThrowsAsync<KavenegarDeliveryException>(() => Create(http).SendAsync("09123456789", "482193"));
        Assert.Equal(transient, error.IsTransient);
    }

    [Fact]
    public async Task Network_errors_are_transient_and_do_not_leak_secrets()
    {
        var handler = new ThrowingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.kavenegar.com/v1/") };
        var error = await Assert.ThrowsAsync<KavenegarDeliveryException>(() => Create(http).SendAsync("09123456789", "482193"));
        Assert.True(error.IsTransient);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "teacher-otp-approved")]
    [InlineData("test-key", "")]
    public async Task Missing_configuration_fails_before_network_call(string apiKey, string template)
    {
        var handler = new CaptureHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.kavenegar.com/v1/") };
        var error = await Assert.ThrowsAsync<KavenegarDeliveryException>(() => Create(http, apiKey, template).SendAsync("09123456789", "482193"));
        Assert.False(error.IsTransient);
        Assert.Equal(0, handler.Calls);
    }

    private static KavenegarOtpSender Create(HttpClient http, string key = "encoded-api-key", string template = "teacher-otp-approved") =>
        new(http, Options.Create(new KavenegarOptions { ApiKey = key, OtpTemplate = template }), NullLogger<KavenegarOtpSender>.Instance);

    private sealed class CaptureHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Method { get; private set; }
        public string? Uri { get; private set; }
        public string? ContentType { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method.Method;
            Uri = request.RequestUri?.ToString();
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("secret key must not leak"));
    }
}
