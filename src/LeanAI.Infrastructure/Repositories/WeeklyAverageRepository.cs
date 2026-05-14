using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Repositories;

public class WeeklyAverageRepository(LeanAIDbContext context) : IWeeklyAverageRepository
{
    public async Task<IReadOnlyList<WeeklyAverage>> GetRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await context.WeeklyAverages
            .Where(e => e.WeekStart >= from && e.WeekStart <= to)
            .OrderBy(e => e.WeekStart)
            .ToListAsync(ct);

    public async Task UpsertAsync(WeeklyAverage entry, CancellationToken ct = default)
    {
        var existing = await context.WeeklyAverages
            .FirstOrDefaultAsync(e => e.WeekStart == entry.WeekStart, ct);

        if (existing is null)
            context.WeeklyAverages.Add(entry);
        else
            existing.AverageWeightKg = entry.AverageWeightKg;

        await context.SaveChangesAsync(ct);
    }
}
