using LeanAI.Domain.EnergyTracking.Entities;

namespace LeanAI.Domain.EnergyTracking.Interfaces;

public interface IEnergyLogRepository
{
    // Returns net calories (food − activity − bmr) for the date, or null when no data exists.
    Task<double?> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default);
    Task<EnergyLog> AddAsync(EnergyLog log, CancellationToken ct = default);
    Task<EnergyLog> AddActivityAsync(DateOnly date, double calories, string description, CancellationToken ct = default);
    Task DeleteActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<EnergyLog>> GetActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<EnergyLog?> GetBmrForDateAsync(DateOnly date, CancellationToken ct = default);
    Task UpsertBmrAsync(DateOnly date, double calories, CancellationToken ct = default);
    Task<EnergyLog> AddRoutineCopyAsync(EnergyLog log, CancellationToken ct = default);
    Task<IReadOnlyList<EnergyLog>> GetRoutineCopiesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<EnergyLog>> GetManyByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task DeleteAsync(EnergyLog log, CancellationToken ct = default);
    Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
