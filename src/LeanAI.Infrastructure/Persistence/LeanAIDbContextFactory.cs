using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeanAI.Infrastructure.Persistence;

public class LeanAIDbContextFactory : IDesignTimeDbContextFactory<LeanAIDbContext>
{
    public LeanAIDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LeanAIDbContext>()
            .UseSqlite("Data Source=leanai_design.db")
            .Options;

        return new LeanAIDbContext(options);
    }
}
