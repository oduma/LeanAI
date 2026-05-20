using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class RoutineRepository(LeanAIDbContext context) : IRoutineRepository
{
    public async Task<IReadOnlyList<RoutineItem>> GetAllAsync(CancellationToken ct = default)
        => await context.RoutineItems
                        .OrderBy(r => r.SourceType)
                        .ThenBy(r => r.Id)
                        .ToListAsync(ct);

    public async Task ReplaceAllAsync(IReadOnlyList<RoutineItem> items, CancellationToken ct = default)
    {
        var existing = await context.RoutineItems.ToListAsync(ct);
        context.RoutineItems.RemoveRange(existing);
        context.RoutineItems.AddRange(items);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> GetIsActiveForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var status = await context.DailyRoutineStatuses
                                  .Where(s => s.Date == date)
                                  .FirstOrDefaultAsync(ct);
        return status?.IsActive ?? false;
    }

    public async Task SetIsActiveForDateAsync(DateOnly date, bool isActive, CancellationToken ct = default)
    {
        var existing = await context.DailyRoutineStatuses
                                    .Where(s => s.Date == date)
                                    .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            existing.IsActive = isActive;
        }
        else
        {
            context.DailyRoutineStatuses.Add(new DailyRoutineStatus { Date = date, IsActive = isActive });
        }
        await context.SaveChangesAsync(ct);
    }
}
