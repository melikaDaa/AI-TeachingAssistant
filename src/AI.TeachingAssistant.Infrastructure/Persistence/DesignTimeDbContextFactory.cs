using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AI.TeachingAssistant.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` create migrations without booting the API host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=madreseai.db")
            .Options;
        return new AppDbContext(options);
    }
}
