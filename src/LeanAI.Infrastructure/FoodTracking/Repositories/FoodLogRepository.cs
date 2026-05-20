using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class FoodLogRepository(LeanAIDbContext context) : IFoodLogRepository
{
    public async Task<IReadOnlyList<FoodLog>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
        => await context.FoodLogs
                        .Include(fl => fl.CaloryLog)
                        .Where(fl => fl.Date == date)
                        .ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<FoodLog> logs, CancellationToken ct = default)
    {
        context.FoodLogs.AddRange(logs);
        await context.SaveChangesAsync(ct);
    }

    public async Task<FoodLog?> GetByCaloryLogIdAsync(Guid caloryLogId, CancellationToken ct = default)
        => await context.FoodLogs
                        .Include(fl => fl.CaloryLog)
                        .Where(fl => fl.CaloryLogId == caloryLogId)
                        .FirstOrDefaultAsync(ct);

    public async Task DeleteByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var logs = await context.FoodLogs
                                .Include(fl => fl.CaloryLog)
                                .Where(fl => fl.Date == date)
                                .ToListAsync(ct);
        if (logs.Count == 0) return;

        // FoodLog → CaloryLog FK cascades parent→child (CaloryLog deletion removes FoodLog),
        // so deleting FoodLog alone leaves CaloryLog orphaned. Remove both together.
        context.FoodLogs.RemoveRange(logs);
        context.CaloryLogs.RemoveRange(logs.Select(fl => fl.CaloryLog));
        await context.SaveChangesAsync(ct);
    }
}
