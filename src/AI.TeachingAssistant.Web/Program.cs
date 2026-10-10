using AI.TeachingAssistant.Infrastructure;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Serve the REST API from the same host so one `dotnet run` runs the whole MVP.
// Controllers live in the Api project; register that assembly for controller discovery.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AI.TeachingAssistant.Api.Controllers.LessonsController).Assembly);

// Friendly Persian validation errors instead of the default English ProblemDetails.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        var message = errors.Count > 0 && errors.All(e => IsPersian(e))
            ? string.Join(" ", errors)
            : "لطفاً اطلاعات وارد شده را بررسی و دوباره تلاش کنید.";
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = message });
    };
});

static bool IsPersian(string text) => text.Any(c => c > 0x0600 && c < 0x06FF);

builder.Services.AddTeachingAssistantInfrastructure(builder.Configuration);

builder.Services.AddScoped<AI.TeachingAssistant.Web.Services.MadreseAIApiClient>(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().BaseUri) };
    return new AI.TeachingAssistant.Web.Services.MadreseAIApiClient(http);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // The UI ships a single mutable app.css (and other non-fingerprinted assets).
        // Without explicit cache headers browsers apply heuristic freshness and can keep
        // a stale stylesheet across UI updates — which silently breaks responsive nav.
        // Force revalidation (cheap 304s via ETag) so updated CSS always wins.
        var name = ctx.File.Name;
        if (name.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
        }
    }
});
app.UseAntiforgery();

// Same Persian error envelope as the standalone Api host (spec §18).
app.UseMiddleware<AI.TeachingAssistant.Api.Middleware.ErrorHandlingMiddleware>();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
