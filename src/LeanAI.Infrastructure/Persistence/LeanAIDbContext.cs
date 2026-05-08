using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Persistence;

public class LeanAIDbContext(DbContextOptions<LeanAIDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
