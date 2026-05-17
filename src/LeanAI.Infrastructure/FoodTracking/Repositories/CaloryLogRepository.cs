using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.FoodTracking.Repositories;

internal sealed class CaloryLogRepository(LeanAIDbContext context) : ICaloryLogRepository
{
    public Task<double> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default)
        => context.CaloryLogs
                  .Where(cl => cl.Date == date)
                  .SumAsync(cl => cl.Calories, ct);
}
