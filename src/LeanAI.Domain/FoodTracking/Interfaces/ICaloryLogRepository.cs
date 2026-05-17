using LeanAI.Domain.FoodTracking.Entities;

namespace LeanAI.Domain.FoodTracking.Interfaces;

public interface ICaloryLogRepository
{
    Task<double> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default);
}
