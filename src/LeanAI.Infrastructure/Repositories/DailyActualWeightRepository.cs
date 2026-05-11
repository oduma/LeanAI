using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Repositories;

public class DailyActualWeightRepository(LeanAIDbContext context) : IDailyActualWeightRepository
{
    public Task<DailyActualWeight?> GetByDateAsync(DateOnly date, CancellationToken ct = default) =>
        context.DailyActualWeights.FirstOrDefaultAsync(e => e.Date == date, ct);

    public async Task<IReadOnlyList<DailyActualWeight>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await context.DailyActualWeights
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

    public async Task UpsertAsync(DailyActualWeight entry, CancellationToken ct = default)
    {
        var tracked = context.ChangeTracker.Entries<DailyActualWeight>()
            .Any(e => e.Entity.Id == entry.Id);

        if (!tracked)
        {
            var exists = await context.DailyActualWeights
                .AsNoTracking()
                .AnyAsync(e => e.Id == entry.Id, ct);

            if (exists)
                context.Update(entry);
            else
                context.Add(entry);
        }

        await context.SaveChangesAsync(ct);
    }
}
