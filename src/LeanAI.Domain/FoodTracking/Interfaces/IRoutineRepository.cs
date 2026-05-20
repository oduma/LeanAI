using LeanAI.Domain.FoodTracking.Entities;

namespace LeanAI.Domain.FoodTracking.Interfaces;

public interface IRoutineRepository
{
    Task<IReadOnlyList<RoutineItem>> GetAllAsync(CancellationToken ct = default);
    Task ReplaceAllAsync(IReadOnlyList<RoutineItem> items, CancellationToken ct = default);
    Task<bool> GetIsActiveForDateAsync(DateOnly date, CancellationToken ct = default);
    Task SetIsActiveForDateAsync(DateOnly date, bool isActive, CancellationToken ct = default);
}
