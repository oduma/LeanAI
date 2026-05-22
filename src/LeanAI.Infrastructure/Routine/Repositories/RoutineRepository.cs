using LeanAI.Domain.Routine.Entities;
using LeanAI.Domain.Routine.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Routine.Repositories;

internal sealed class RoutineRepository(LeanAIDbContext context) : IRoutineRepository
{
    public async Task<IReadOnlyList<RoutineFoodItem>> GetAllFoodItemsAsync(CancellationToken ct = default)
        => await context.RoutineFoodItems
                        .OrderBy(r => r.Id)
                        .ToListAsync(ct);

    public async Task<IReadOnlyList<RoutineActivityItem>> GetAllActivityItemsAsync(CancellationToken ct = default)
        => await context.RoutineActivityItems
                        .OrderBy(r => r.Id)
                        .ToListAsync(ct);

    public async Task ReplaceAllAsync(
        IReadOnlyList<RoutineFoodItem>     foodItems,
        IReadOnlyList<RoutineActivityItem> activityItems,
        CancellationToken                  ct = default)
    {
        var existingFood     = await context.RoutineFoodItems.ToListAsync(ct);
        var existingActivity = await context.RoutineActivityItems.ToListAsync(ct);
        context.RoutineFoodItems.RemoveRange(existingFood);
        context.RoutineActivityItems.RemoveRange(existingActivity);
        context.RoutineFoodItems.AddRange(foodItems);
        context.RoutineActivityItems.AddRange(activityItems);
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
