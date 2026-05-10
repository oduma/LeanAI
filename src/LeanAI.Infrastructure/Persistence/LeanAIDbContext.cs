using LeanAI.Domain.WeightManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Persistence;

public class LeanAIDbContext(DbContextOptions<LeanAIDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

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
    }
}
