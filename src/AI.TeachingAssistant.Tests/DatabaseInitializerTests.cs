using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_AppliesMigrationsWithoutCreatingDemoUsers()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        await using (var db = new AppDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(db);
            Assert.Empty(await db.Teachers.ToListAsync());
            Assert.Empty(await db.Lessons.ToListAsync());
        }

        // Repeated startup must not duplicate seeded rows.
        await using (var db = new AppDbContext(options))
        {
            await DatabaseInitializer.InitializeAsync(db);
            Assert.Empty(await db.Teachers.ToListAsync());
            Assert.Empty(await db.Lessons.ToListAsync());
        }
    }
}
