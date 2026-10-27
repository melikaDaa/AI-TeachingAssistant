using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;

namespace AI.TeachingAssistant.Web.Services;

public sealed class InternalAssertionHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authentication;
    private readonly IConfiguration _configuration;

    public InternalAssertionHandler(AuthenticationStateProvider authentication, IConfiguration configuration)
    {
        _authentication = authentication;
        _configuration = configuration;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var principal = (await _authentication.GetAuthenticationStateAsync()).User;
        var teacherId = principal.FindFirst("teacher_id")?.Value;
        if (string.IsNullOrWhiteSpace(teacherId) || !int.TryParse(teacherId, out var id) || id <= 0 ||
            principal.FindFirst("iss")?.Value != "madreseai-web" ||
            principal.FindFirst("aud")?.Value != "madreseai-api")
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);

        var expiry = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();

        var secret = _configuration["InternalAssertion:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

        var method = request.Method.Method.ToUpperInvariant();
        var path = request.RequestUri?.PathAndQuery ?? "/";
        var nonce = Guid.NewGuid().ToString("N");
        const string issuer = "madreseai-web";
        const string audience = "madreseai-api";
        var payload = $"{teacherId}\n{issuer}\n{audience}\n{expiry}\n{nonce}\n{method}\n{path}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
        request.Headers.Add("X-MadreseAI-Teacher", teacherId);
        request.Headers.Add("X-MadreseAI-Expiry", expiry.ToString());
        request.Headers.Add("X-MadreseAI-Issuer", issuer);
        request.Headers.Add("X-MadreseAI-Audience", audience);
        request.Headers.Add("X-MadreseAI-Nonce", nonce);
        request.Headers.Add("X-MadreseAI-Signature", signature);
        return await base.SendAsync(request, cancellationToken);
    }
}
