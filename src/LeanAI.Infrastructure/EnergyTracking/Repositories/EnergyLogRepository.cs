using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.EnergyTracking.Repositories;

internal sealed class EnergyLogRepository(LeanAIDbContext context) : IEnergyLogRepository
{
    public async Task<double?> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default)
    {
        var hasFoodData   = await context.FoodLogs.AnyAsync(fl => fl.Date == date, ct);
        var hasEnergyData = await context.EnergyLogs.AnyAsync(el => el.Date == date, ct);
        if (!hasFoodData && !hasEnergyData) return null;

        var foodEnergyIds = await context.FoodLogs
                                         .Where(fl => fl.Date == date)
                                         .Select(fl => fl.EnergyLogId)
                                         .ToListAsync(ct);

        var food = foodEnergyIds.Count > 0
            ? await context.EnergyLogs
                           .Where(el => foodEnergyIds.Contains(el.Id))
                           .SumAsync(el => el.Calories, ct)
            : 0.0;

        var burned = await context.EnergyLogs
                                  .Where(el => el.Date == date
                                            && (el.SourceType == "activity" || el.SourceType == "bmr"))
                                  .SumAsync(el => el.Calories, ct);

        return food - burned;
    }

    public async Task<EnergyLog> AddAsync(EnergyLog log, CancellationToken ct = default)
    {
        context.EnergyLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log;
    }

    public async Task<EnergyLog> AddActivityAsync(
        DateOnly date, double calories, string description, CancellationToken ct = default)
    {
        var log = new EnergyLog
        {
            Date        = date,
            Calories    = calories,
            SourceType  = "activity",
            Description = description
        };
        context.EnergyLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log;
    }

    public async Task DeleteActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var logs = await context.EnergyLogs
                                .Where(el => el.Date == date && el.SourceType == "activity")
                                .ToListAsync(ct);
        if (logs.Count > 0)
        {
            context.EnergyLogs.RemoveRange(logs);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<EnergyLog>> GetActivityCaloriesForDateAsync(
        DateOnly date, CancellationToken ct = default)
        => await context.EnergyLogs
                        .Where(el => el.Date == date && el.SourceType == "activity")
                        .OrderBy(el => el.Id)
                        .ToListAsync(ct);

    public async Task<EnergyLog?> GetBmrForDateAsync(DateOnly date, CancellationToken ct = default)
        => await context.EnergyLogs
                        .Where(el => el.Date == date && el.SourceType == "bmr")
                        .FirstOrDefaultAsync(ct);

    public async Task UpsertBmrAsync(DateOnly date, double calories, CancellationToken ct = default)
    {
        var existing = await context.EnergyLogs
                                    .Where(el => el.Date == date && el.SourceType == "bmr")
                                    .FirstOrDefaultAsync(ct);
        if (existing is not null)
            context.EnergyLogs.Remove(existing);

        context.EnergyLogs.Add(new EnergyLog
        {
            Date       = date,
            Calories   = calories,
            SourceType = "bmr"
        });
        await context.SaveChangesAsync(ct);
    }

    public async Task<EnergyLog> AddRoutineCopyAsync(EnergyLog log, CancellationToken ct = default)
    {
        context.EnergyLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IReadOnlyList<EnergyLog>> GetRoutineCopiesForDateAsync(
        DateOnly date, CancellationToken ct = default)
        => await context.EnergyLogs
                        .Where(el => el.Date == date && el.RoutineItemId != null)
                        .ToListAsync(ct);

    public async Task<IReadOnlyList<EnergyLog>> GetManyByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        return await context.EnergyLogs.Where(el => ids.Contains(el.Id)).ToListAsync(ct);
    }

    public async Task DeleteAsync(EnergyLog log, CancellationToken ct = default)
    {
        context.EnergyLogs.Remove(log);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        var logs = await context.EnergyLogs.Where(el => ids.Contains(el.Id)).ToListAsync(ct);
        if (logs.Count > 0)
        {
            context.EnergyLogs.RemoveRange(logs);
            await context.SaveChangesAsync(ct);
        }
    }
}
