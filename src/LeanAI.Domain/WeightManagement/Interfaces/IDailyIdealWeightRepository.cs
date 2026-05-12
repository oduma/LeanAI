using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IDailyIdealWeightRepository
{
    Task DeleteAllAsync(CancellationToken ct = default);
    Task InsertBatchAsync(IEnumerable<DailyIdealWeight> entries, CancellationToken ct = default);
    Task<DailyIdealWeight?> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailyIdealWeight>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
