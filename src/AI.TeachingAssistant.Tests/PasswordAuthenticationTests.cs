using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class PasswordAuthenticationTests
{
    [Fact]
    public async Task Registration_hashes_password_and_login_accepts_case_insensitive_email()
    {
        await using var fixture = await Fixture.CreateAsync();
        var account = await fixture.Service.RegisterAsync(new("Teacher One", "Teacher@example.com", "LongPassword123", "LongPassword123"), default);
        Assert.NotEqual("LongPassword123", account.PasswordHash);
        Assert.StartsWith("AQAAAA", account.PasswordHash);
        Assert.Equal(account.Id, (await fixture.Service.LoginAsync(new("teacher@example.com", "LongPassword123"), default)).Id);
        await Assert.ThrowsAsync<AccountException>(() => fixture.Service.LoginAsync(new("teacher@example.com", "wrong-password"), default));
    }

    [Fact]
    public async Task Duplicate_email_and_invalid_password_are_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.RegisterAsync(new("Teacher One", "teacher@example.com", "LongPassword123", "LongPassword123"), default);
        var duplicate = await Assert.ThrowsAsync<AccountException>(() => fixture.Service.RegisterAsync(new("Teacher Two", "TEACHER@example.com", "LongPassword123", "LongPassword123"), default));
        Assert.Equal(409, duplicate.StatusCode);
        await Assert.ThrowsAsync<AccountException>(() => fixture.Service.RegisterAsync(new("Teacher Two", "teacher2@example.com", "short", "short"), default));
    }

    [Fact]
    public async Task Invalid_email_is_rejected_and_phone_registration_is_not_enabled()
    {
        await using var fixture = await Fixture.CreateAsync();
        var error = await Assert.ThrowsAsync<AccountException>(() => fixture.Service.RegisterAsync(new("Teacher", "not-email", "LongPassword123", "LongPassword123"), default));
        Assert.Equal(400, error.StatusCode);
        var phone = await Assert.ThrowsAsync<AccountException>(() => fixture.Service.RegisterAsync(new("Teacher", "09123456789", "LongPassword123", "LongPassword123"), default));
        Assert.Equal(400, phone.StatusCode);
    }

    [Fact]
    public async Task Registration_preserves_teacher_identity_and_unrelated_lessons()
    {
        await using var fixture = await Fixture.CreateAsync();
        var account = await fixture.Service.RegisterAsync(new("Teacher One", "teacher@example.com", "LongPassword123", "LongPassword123"), default);
        var lesson = new Lesson { TeacherId = account.Id, Title = "Existing", Subject = "Science", Grade = "7", Topic = "Topic", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        fixture.Db.Lessons.Add(lesson);
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(account.Id, (await fixture.Db.Lessons.SingleAsync()).TeacherId);
        Assert.Null(account.PhoneNumber);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AppDbContext _db;
        public AppDbContext Db => _db;
        public PasswordAuthenticationService Service { get; }
        private Fixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection; _db = db;
            Service = new PasswordAuthenticationService(db, new PasswordHasher<Teacher>());
        }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options); await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }
        public async ValueTask DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
