using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IDailyIdealWeightRepository
{
    Task DeleteAllAsync(CancellationToken ct = default);
    Task InsertBatchAsync(IEnumerable<DailyIdealWeight> entries, CancellationToken ct = default);
}
