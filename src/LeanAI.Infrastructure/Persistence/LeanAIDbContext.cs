using LeanAI.Domain.WeightManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Persistence;

public class LeanAIDbContext(DbContextOptions<LeanAIDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile>      UserProfiles      => Set<UserProfile>();
    public DbSet<DailyIdealWeight> DailyIdealWeights => Set<DailyIdealWeight>();
    public DbSet<DailyActualWeight> DailyActualWeights => Set<DailyActualWeight>();

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
    }
}
