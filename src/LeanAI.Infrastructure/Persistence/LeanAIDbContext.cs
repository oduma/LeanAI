using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.WeightManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Persistence;

public class LeanAIDbContext(DbContextOptions<LeanAIDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile>       UserProfiles       => Set<UserProfile>();
    public DbSet<DailyIdealWeight>  DailyIdealWeights  => Set<DailyIdealWeight>();
    public DbSet<DailyActualWeight> DailyActualWeights => Set<DailyActualWeight>();
    public DbSet<AppSettings>       AppSettings        => Set<AppSettings>();
    public DbSet<WeeklyAverage>     WeeklyAverages     => Set<WeeklyAverage>();
    public DbSet<ActivityLog>       ActivityLogs       => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
            entity.Property(e => e.UnitSystem).IsRequired();
            entity.Property(e => e.Gender);
            entity.Property(e => e.Age);
            entity.Property(e => e.HeightCm);
            entity.Property(e => e.StartingWeightKg);
            entity.Property(e => e.TargetWeightKg);
            entity.Property(e => e.TargetPeriod);
            entity.Property(e => e.GoalStartDate)
                  .HasConversion(d => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : null,
                                 s => s != null ? DateOnly.Parse(s) : (DateOnly?)null);
            entity.Property(e => e.GoalEndDate)
                  .HasConversion(d => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : null,
                                 s => s != null ? DateOnly.Parse(s) : (DateOnly?)null);
        });

        modelBuilder.Entity<DailyIdealWeight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
            entity.Property(e => e.Date).IsRequired()
                  .HasConversion(d => d.ToString("yyyy-MM-dd"),
                                 s => DateOnly.Parse(s));
            entity.Property(e => e.WeightKg).IsRequired();
        });

        modelBuilder.Entity<DailyActualWeight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
            entity.Property(e => e.Date).IsRequired()
                  .HasConversion(d => d.ToString("yyyy-MM-dd"),
                                 s => DateOnly.Parse(s));
            entity.Property(e => e.WeightKg).IsRequired();
            entity.Property(e => e.Notes);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
            entity.Property(e => e.GeminiModelName).IsRequired();
            entity.Property(e => e.CalendarFirstDay)
                  .HasConversion<int>()
                  .IsRequired();
        });

        modelBuilder.Entity<WeeklyAverage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
            entity.Property(e => e.WeekStart).IsRequired()
                  .HasConversion(d => d.ToString("yyyy-MM-dd"),
                                 s => DateOnly.Parse(s));
            entity.HasIndex(e => e.WeekStart).IsUnique();
            entity.Property(e => e.AverageWeightKg).IsRequired();
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DomainEvents);
            entity.Property(e => e.Date).IsRequired()
                  .HasConversion(d => d.ToString("yyyy-MM-dd"),
                                 s => DateOnly.Parse(s));
            entity.Property(e => e.Activity).IsRequired();
            entity.Property(e => e.ParameterName).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Unit).IsRequired();
        });
    }
}
