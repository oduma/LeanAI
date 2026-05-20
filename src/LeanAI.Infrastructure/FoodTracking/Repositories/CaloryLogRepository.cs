using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class CaloryLogRepository(LeanAIDbContext context) : ICaloryLogRepository
{
    public async Task<double> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default)
    {
        // Food: join through FoodLogs so orphaned CaloryLog rows are never counted
        var food     = await context.FoodLogs
                                    .Where(fl => fl.Date == date)
                                    .SumAsync(fl => fl.CaloryLog.Calories, ct);
        var activity = await context.CaloryLogs
                                    .Where(cl => cl.Date == date && cl.SourceType == "activity")
                                    .SumAsync(cl => cl.Calories, ct);
        return food - activity;
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
}
