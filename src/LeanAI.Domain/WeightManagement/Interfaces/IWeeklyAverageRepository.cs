using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IWeeklyAverageRepository
{
    Task<IReadOnlyList<WeeklyAverage>> GetRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default);

    Task UpsertAsync(WeeklyAverage entry, CancellationToken ct = default);
}
