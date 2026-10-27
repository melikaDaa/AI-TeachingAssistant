using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class MigrationUpgradeTests
{
    [Fact]
    public async Task Existing_database_data_survives_upgrade_to_durable_jobs_schema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"migration-upgrade-{Guid.NewGuid():N}.db");
        var cs = $"Data Source={path};Default Timeout=5";
        try
        {
            int teacherId, lessonId;
            await using (var db = NewDb(cs))
            {
                await db.Database.MigrateAsync("20261024105427_UsageTokenAccounting");
                var teacher = new Teacher { FullName = "Upgrade", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" };
                db.Teachers.Add(teacher); await db.SaveChangesAsync(); teacherId = teacher.Id;
                var lesson = new Lesson { TeacherId = teacherId, Title = "Existing", Subject = "Science", Grade = "1", Topic = "Topic", DurationMinutes = 40, StudentLevel = "x" };
                db.Lessons.Add(lesson); await db.SaveChangesAsync(); lessonId = lesson.Id;
            }
            await using (var upgraded = NewDb(cs))
            {
                await DatabaseInitializer.InitializeAsync(upgraded);
                Assert.Equal("Upgrade", (await upgraded.Teachers.SingleAsync(x => x.Id == teacherId)).FullName);
                Assert.Equal("Existing", (await upgraded.Lessons.SingleAsync(x => x.Id == lessonId)).Title);
                Assert.Empty(await upgraded.GenerationJobs.ToListAsync());
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using var command = upgraded.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA table_info('UsageEvents')";
                if (command.Connection!.State != System.Data.ConnectionState.Open) await command.Connection.OpenAsync();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
                Assert.Contains("ProviderCostMicros", columns);
                Assert.Contains("OutcomeDetail", columns);
            }
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    private static AppDbContext NewDb(string cs) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options);
}
