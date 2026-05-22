using LeanAI.Application.EnergyTracking.DTOs;
using LeanAI.Domain.EnergyTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.EnergyTracking.Queries.GetActivityCaloriesForDate;

public sealed class GetActivityCaloriesForDateQueryHandler(IEnergyLogRepository repo)
    : IRequestHandler<GetActivityCaloriesForDateQuery, IReadOnlyList<ActivityEnergyLogDto>>
{
    public async Task<IReadOnlyList<ActivityEnergyLogDto>> Handle(
        GetActivityCaloriesForDateQuery request,
        CancellationToken               cancellationToken)
    {
        var logs = await repo.GetActivityCaloriesForDateAsync(request.Date, cancellationToken);
        return logs
            .Select(l => new ActivityEnergyLogDto(l.Id, l.Description, l.Calories))
            .ToList();
    }
}
