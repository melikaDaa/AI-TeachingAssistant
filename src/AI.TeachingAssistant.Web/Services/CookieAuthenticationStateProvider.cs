using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AI.TeachingAssistant.Web.Services;

public sealed class CookieAuthenticationStateProvider(IHttpContextAccessor accessor) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var principal = accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(principal));
    }
}
