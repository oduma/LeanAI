using LeanAI.Domain.Routine.Entities;

namespace LeanAI.Domain.Routine.Interfaces;

public interface IRoutineRepository
{
    Task<IReadOnlyList<RoutineFoodItem>>     GetAllFoodItemsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoutineActivityItem>> GetAllActivityItemsAsync(CancellationToken ct = default);
    Task ReplaceAllAsync(
        IReadOnlyList<RoutineFoodItem>     foodItems,
        IReadOnlyList<RoutineActivityItem> activityItems,
        CancellationToken                  ct = default);
    Task<bool> GetIsActiveForDateAsync(DateOnly date, CancellationToken ct = default);
    Task SetIsActiveForDateAsync(DateOnly date, bool isActive, CancellationToken ct = default);
}
