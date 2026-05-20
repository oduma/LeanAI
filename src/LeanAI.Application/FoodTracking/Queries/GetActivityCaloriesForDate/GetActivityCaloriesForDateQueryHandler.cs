using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetActivityCaloriesForDate;

public sealed class GetActivityCaloriesForDateQueryHandler(ICaloryLogRepository repo)
    : IRequestHandler<GetActivityCaloriesForDateQuery, IReadOnlyList<ActivityCaloryLogDto>>
{
    public async Task<IReadOnlyList<ActivityCaloryLogDto>> Handle(
        GetActivityCaloriesForDateQuery request,
        CancellationToken               cancellationToken)
    {
        var logs = await repo.GetActivityCaloriesForDateAsync(request.Date, cancellationToken);
        return logs
            .Select(l => new ActivityCaloryLogDto(l.Id, l.Description, l.Calories))
            .ToList();
    }
}
