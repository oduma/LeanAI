using LeanAI.Domain.ActivityTracking.Entities;

namespace LeanAI.Domain.ActivityTracking.Interfaces;

public interface ICustomActivityLogRepository
{
    Task AddAsync(CustomActivityLog log, CancellationToken ct = default);
    Task DeleteForDateAsync(DateOnly date, CancellationToken ct = default);
    Task DeleteByEnergyLogIdsAsync(IReadOnlyList<Guid> energyLogIds, CancellationToken ct = default);
}
