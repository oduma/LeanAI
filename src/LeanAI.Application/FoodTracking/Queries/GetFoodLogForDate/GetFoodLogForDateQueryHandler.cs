using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;

public sealed class GetFoodLogForDateQueryHandler(IFoodLogRepository repo)
    : IRequestHandler<GetFoodLogForDateQuery, IReadOnlyList<FoodLogEntryDto>>
{
    public async Task<IReadOnlyList<FoodLogEntryDto>> Handle(GetFoodLogForDateQuery request, CancellationToken cancellationToken)
    {
        var logs = await repo.GetByDateAsync(request.Date, cancellationToken);
        return logs.Select(fl => new FoodLogEntryDto(fl.Id, fl.CaloryLogId, fl.FoodItem, fl.Quantity, fl.CaloryLog.Calories))
                   .ToList();
    }
}
