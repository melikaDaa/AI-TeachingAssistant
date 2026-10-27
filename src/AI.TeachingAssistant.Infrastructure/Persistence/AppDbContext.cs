using AI.TeachingAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI.TeachingAssistant.Infrastructure.Persistence;

/// <summary>EF Core database context. Simple schema; generated payloads are stored as JSON text.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<TeachingPackage> TeachingPackages => Set<TeachingPackage>();
    public DbSet<LessonPlan> LessonPlans => Set<LessonPlan>();
    public DbSet<EducationalContent> EducationalContents => Set<EducationalContent>();
    public DbSet<ClassroomActivity> ClassroomActivities => Set<ClassroomActivity>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<MediaPrompt> MediaPrompts => Set<MediaPrompt>();
    public DbSet<PhoneVerificationChallenge> PhoneVerificationChallenges => Set<PhoneVerificationChallenge>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<GenerationJobRecord> GenerationJobs => Set<GenerationJobRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Teacher>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.Level).HasMaxLength(50);
            e.Property(x => x.Grade).HasMaxLength(50);
            e.Property(x => x.Subjects).HasMaxLength(200);
            e.Property(x => x.TeachingStyle).HasMaxLength(100);
            e.Property(x => x.PhoneNumber).HasMaxLength(16);
            e.HasIndex(x => x.PhoneNumber).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.PasswordResetTokenHash).HasMaxLength(64);
        });

        b.Entity<PhoneVerificationChallenge>(e =>
        {
            e.Property(x => x.PhoneNumber).HasMaxLength(16).IsRequired();
            e.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.RequestIpHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("\"ConsumedAt\" IS NULL");
            e.HasIndex(x => new { x.PhoneNumber, x.CreatedAt });
        });

        b.Entity<UsageEvent>(e =>
        {
            e.Property(x => x.Operation).HasMaxLength(40).IsRequired();
            e.Property(x => x.RequestKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(80);
            e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.CostStatus).HasMaxLength(30).IsRequired();
            e.Property(x => x.OutcomeDetail).HasMaxLength(80);
            e.HasIndex(x => x.RequestKey).IsUnique();
            e.HasIndex(x => new { x.TeacherId, x.CreatedAtUtc });
            e.HasOne<Teacher>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GenerationJobRecord>(e =>
        {
            e.Property(x => x.RequestKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.OwnerId).HasMaxLength(100);
            e.Property(x => x.FailureCode).HasMaxLength(80);
            e.HasIndex(x => x.RequestKey).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        });

        b.Entity<UsageEvent>(e =>
        {
            e.Property(x => x.Provider).HasMaxLength(80);
            e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.CostStatus).HasMaxLength(30).IsRequired();
            e.Property(x => x.OutcomeDetail).HasMaxLength(80);
        });

        b.Entity<Lesson>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Subject).HasMaxLength(100);
            e.Property(x => x.Grade).HasMaxLength(50);
            e.Property(x => x.Topic).HasMaxLength(300);
            e.Property(x => x.StudentLevel).HasMaxLength(50);
            e.Property(x => x.SessionStyle).HasMaxLength(100);
            e.Property(x => x.AvailableResources).HasMaxLength(300);
            e.Property(x => x.RequestedSections).HasMaxLength(50);
            e.Property(x => x.LastError).HasMaxLength(500);
            e.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TeacherId, x.CreatedAt });
        });

        b.Entity<TeachingPackage>(e =>
        {
            e.Property(x => x.CompletedSections).HasMaxLength(100);
            e.HasOne(x => x.Lesson)
                .WithOne(x => x.Package)
                .HasForeignKey<TeachingPackage>(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LessonPlan>(e =>
        {
            e.HasOne(x => x.TeachingPackage)
                .WithOne(x => x.LessonPlan)
                .HasForeignKey<LessonPlan>(x => x.TeachingPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EducationalContent>(e =>
        {
            e.HasOne(x => x.TeachingPackage)
                .WithOne(x => x.EducationalContent)
                .HasForeignKey<EducationalContent>(x => x.TeachingPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClassroomActivity>(e =>
        {
            e.HasOne(x => x.TeachingPackage)
                .WithOne(x => x.Activity)
                .HasForeignKey<ClassroomActivity>(x => x.TeachingPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Quiz>(e =>
        {
            e.HasOne(x => x.TeachingPackage)
                .WithOne(x => x.Quiz)
                .HasForeignKey<Quiz>(x => x.TeachingPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MediaPrompt>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasOne(x => x.TeachingPackage)
                .WithMany(x => x.MediaPrompts)
                .HasForeignKey(x => x.TeachingPackageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
