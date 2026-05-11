using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Repositories;

public class DailyIdealWeightRepository(LeanAIDbContext context) : IDailyIdealWeightRepository
{
    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync("DELETE FROM DailyIdealWeights", ct);
    }

    public async Task InsertBatchAsync(IEnumerable<DailyIdealWeight> entries, CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        context.DailyIdealWeights.AddRange(entries);
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public Task<DailyIdealWeight?> GetByDateAsync(DateOnly date, CancellationToken ct = default) =>
        context.DailyIdealWeights.FirstOrDefaultAsync(e => e.Date == date, ct);
}
