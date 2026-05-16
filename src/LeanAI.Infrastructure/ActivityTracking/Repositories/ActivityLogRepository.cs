using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;

namespace LeanAI.Infrastructure.ActivityTracking.Repositories;

internal sealed class ActivityLogRepository(LeanAIDbContext context) : IActivityLogRepository
{
    public async Task AddRangeAsync(IEnumerable<ActivityLog> logs, CancellationToken ct = default)
    {
        context.ActivityLogs.AddRange(logs);
        await context.SaveChangesAsync(ct);
    }
}
