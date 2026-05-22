using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.ActivityTracking.Repositories;

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

    public async Task DeleteByEnergyLogIdsAsync(IReadOnlyList<Guid> energyLogIds, CancellationToken ct = default)
    {
        if (energyLogIds.Count == 0) return;
        var logs = await context.CustomActivityLogs
                                .Where(l => energyLogIds.Contains(l.EnergyLogId))
                                .ToListAsync(ct);
        if (logs.Count > 0)
        {
            context.CustomActivityLogs.RemoveRange(logs);
            await context.SaveChangesAsync(ct);
        }
    }
}
