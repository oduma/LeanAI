using LeanAI.Domain.FoodTracking.Entities;

namespace LeanAI.Domain.FoodTracking.Interfaces;

public interface ICaloryLogRepository
{
    // Returns net calories (food − activity − bmr) for the date, or null when no data exists.
    Task<double?> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default);
    Task<CaloryLog> AddActivityAsync(DateOnly date, double calories, string description, CancellationToken ct = default);
    Task DeleteActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<CaloryLog>> GetActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<CaloryLog?> GetBmrForDateAsync(DateOnly date, CancellationToken ct = default);
    Task UpsertBmrAsync(DateOnly date, double calories, CancellationToken ct = default);
    Task<CaloryLog> AddRoutineCopyAsync(CaloryLog log, CancellationToken ct = default);
    Task<IReadOnlyList<CaloryLog>> GetRoutineCopiesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task DeleteRoutineCopyAsync(CaloryLog log, CancellationToken ct = default);
}
