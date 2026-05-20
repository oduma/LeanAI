using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class CaloryLogRepository(LeanAIDbContext context) : ICaloryLogRepository
{
    public async Task<double?> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default)
    {
        var hasFoodData   = await context.FoodLogs.AnyAsync(fl => fl.Date == date, ct);
        var hasCaloryData = await context.CaloryLogs.AnyAsync(cl => cl.Date == date, ct);
        if (!hasFoodData && !hasCaloryData) return null;

        // Food: join through FoodLogs so orphaned CaloryLog rows are never counted
        var food   = await context.FoodLogs
                                  .Where(fl => fl.Date == date)
                                  .SumAsync(fl => fl.CaloryLog.Calories, ct);
        var burned = await context.CaloryLogs
                                  .Where(cl => cl.Date == date
                                            && (cl.SourceType == "activity" || cl.SourceType == "bmr"))
                                  .SumAsync(cl => cl.Calories, ct);
        return food - burned;
    }

    public async Task<CaloryLog> AddActivityAsync(
        DateOnly date, double calories, string description, CancellationToken ct = default)
    {
        var log = new CaloryLog
        {
            Date        = date,
            Calories    = calories,
            SourceType  = "activity",
            Description = description
        };
        context.CaloryLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log;
    }

    public async Task DeleteActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var logs = await context.CaloryLogs
                                .Where(cl => cl.Date == date && cl.SourceType == "activity")
                                .ToListAsync(ct);
        if (logs.Count > 0)
        {
            context.CaloryLogs.RemoveRange(logs);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<CaloryLog>> GetActivityCaloriesForDateAsync(
        DateOnly date, CancellationToken ct = default)
        => await context.CaloryLogs
                        .Where(cl => cl.Date == date && cl.SourceType == "activity")
                        .OrderBy(cl => cl.Id)
                        .ToListAsync(ct);

    public async Task<CaloryLog?> GetBmrForDateAsync(DateOnly date, CancellationToken ct = default)
        => await context.CaloryLogs
                        .Where(cl => cl.Date == date && cl.SourceType == "bmr")
                        .FirstOrDefaultAsync(ct);

    public async Task<CaloryLog> AddRoutineCopyAsync(CaloryLog log, CancellationToken ct = default)
    {
        context.CaloryLogs.Add(log);
        await context.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IReadOnlyList<CaloryLog>> GetRoutineCopiesForDateAsync(DateOnly date, CancellationToken ct = default)
        => await context.CaloryLogs
                        .Where(cl => cl.Date == date && cl.RoutineItemId != null)
                        .ToListAsync(ct);

    public async Task DeleteRoutineCopyAsync(CaloryLog log, CancellationToken ct = default)
    {
        // Also remove any linked FoodLog (EF Core in-memory cascade requires both sides tracked)
        var foodLog = await context.FoodLogs.Where(fl => fl.CaloryLogId == log.Id).FirstOrDefaultAsync(ct);
        if (foodLog is not null)
            context.FoodLogs.Remove(foodLog);

        // Also remove any linked CustomActivityLog
        var customLog = await context.CustomActivityLogs.Where(c => c.CaloryLogId == log.Id).FirstOrDefaultAsync(ct);
        if (customLog is not null)
            context.CustomActivityLogs.Remove(customLog);

        context.CaloryLogs.Remove(log);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpsertBmrAsync(DateOnly date, double calories, CancellationToken ct = default)
    {
        var existing = await context.CaloryLogs
                                    .Where(cl => cl.Date == date && cl.SourceType == "bmr")
                                    .FirstOrDefaultAsync(ct);
        if (existing is not null)
            context.CaloryLogs.Remove(existing);

        context.CaloryLogs.Add(new CaloryLog
        {
            Date       = date,
            Calories   = calories,
            SourceType = "bmr"
        });
        await context.SaveChangesAsync(ct);
    }
}
