using LeanAI.Domain.FoodTracking.Entities;

namespace LeanAI.Domain.FoodTracking.Interfaces;

public interface ICustomActivityLogRepository
{
    Task AddAsync(CustomActivityLog log, CancellationToken ct = default);
    Task DeleteForDateAsync(DateOnly date, CancellationToken ct = default);
}
