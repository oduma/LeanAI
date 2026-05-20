using LeanAI.Domain.FoodTracking.Entities;

namespace LeanAI.Domain.FoodTracking.Interfaces;

public interface ICaloryLogRepository
{
    // Returns net calories (food − activity) for the date.
    Task<double> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default);
    Task<CaloryLog> AddActivityAsync(DateOnly date, double calories, string description, CancellationToken ct = default);
    Task DeleteActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<CaloryLog>> GetActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
}
