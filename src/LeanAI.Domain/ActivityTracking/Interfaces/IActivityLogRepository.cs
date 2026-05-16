using LeanAI.Domain.ActivityTracking.Entities;

namespace LeanAI.Domain.ActivityTracking.Interfaces;

public interface IActivityLogRepository
{
    Task AddRangeAsync(IEnumerable<ActivityLog> logs, CancellationToken ct = default);
}
