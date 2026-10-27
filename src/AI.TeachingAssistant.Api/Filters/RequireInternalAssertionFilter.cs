using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;

namespace AI.TeachingAssistant.Api;

public sealed class RequireInternalAssertionFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/account") ||
            context.HttpContext.Request.Path.StartsWithSegments("/health")) return;

        if (!context.HttpContext.Items.ContainsKey("InternalAssertionValidated"))
            context.Result = new UnauthorizedResult();
    }
}
