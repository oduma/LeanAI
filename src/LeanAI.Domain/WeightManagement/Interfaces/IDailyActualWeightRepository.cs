using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IDailyActualWeightRepository
{
    Task<DailyActualWeight?> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailyActualWeight>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task UpsertAsync(DailyActualWeight entry, CancellationToken ct = default);
}
