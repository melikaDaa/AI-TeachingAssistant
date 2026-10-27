using AI.TeachingAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI.TeachingAssistant.Infrastructure.Persistence;

    /// <summary>Applies schema migrations. Demo users are not seeded in the multi-account MVP.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

    }
}
