using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class CustomActivityLogRepository(LeanAIDbContext context) : ICustomActivityLogRepository
{
    public async Task AddAsync(CustomActivityLog log, CancellationToken ct = default)
    {
        context.CustomActivityLogs.Add(log);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var logs = await context.CustomActivityLogs
                                .Where(l => l.Date == date)
                                .ToListAsync(ct);
        if (logs.Count > 0)
        {
            context.CustomActivityLogs.RemoveRange(logs);
            await context.SaveChangesAsync(ct);
        }
    }
}
