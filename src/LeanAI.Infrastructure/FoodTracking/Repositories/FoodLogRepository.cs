using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class FoodLogRepository(LeanAIDbContext context) : IFoodLogRepository
{
    public async Task<IReadOnlyList<FoodLog>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
        => await context.FoodLogs
                        .Where(fl => fl.Date == date)
                        .ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<FoodLog> logs, CancellationToken ct = default)
    {
        context.FoodLogs.AddRange(logs);
        await context.SaveChangesAsync(ct);
    }

    public async Task<FoodLog?> GetByEnergyLogIdAsync(Guid energyLogId, CancellationToken ct = default)
        => await context.FoodLogs
                        .Where(fl => fl.EnergyLogId == energyLogId)
                        .FirstOrDefaultAsync(ct);

    public async Task DeleteByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var logs = await context.FoodLogs
                                .Where(fl => fl.Date == date)
                                .ToListAsync(ct);
        if (logs.Count == 0) return;
        context.FoodLogs.RemoveRange(logs);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteByEnergyLogIdsAsync(IReadOnlyList<Guid> energyLogIds, CancellationToken ct = default)
    {
        if (energyLogIds.Count == 0) return;
        var logs = await context.FoodLogs
                                .Where(fl => energyLogIds.Contains(fl.EnergyLogId))
                                .ToListAsync(ct);
        if (logs.Count > 0)
        {
            context.FoodLogs.RemoveRange(logs);
            await context.SaveChangesAsync(ct);
        }
    }
}
