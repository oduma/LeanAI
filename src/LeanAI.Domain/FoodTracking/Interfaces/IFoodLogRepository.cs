using LeanAI.Domain.FoodTracking.Entities;

namespace LeanAI.Domain.FoodTracking.Interfaces;

public interface IFoodLogRepository
{
    Task<IReadOnlyList<FoodLog>> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<FoodLog> logs, CancellationToken ct = default);
    Task DeleteByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<FoodLog?> GetByEnergyLogIdAsync(Guid energyLogId, CancellationToken ct = default);
    Task DeleteByEnergyLogIdsAsync(IReadOnlyList<Guid> energyLogIds, CancellationToken ct = default);
}
