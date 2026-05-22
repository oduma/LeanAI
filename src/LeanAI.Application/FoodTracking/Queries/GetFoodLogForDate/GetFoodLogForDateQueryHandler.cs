using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;

public sealed class GetFoodLogForDateQueryHandler(IFoodLogRepository foodRepo, IEnergyLogRepository energyRepo)
    : IRequestHandler<GetFoodLogForDateQuery, IReadOnlyList<FoodLogEntryDto>>
{
    public async Task<IReadOnlyList<FoodLogEntryDto>> Handle(GetFoodLogForDateQuery request, CancellationToken cancellationToken)
    {
        var foodLogs = await foodRepo.GetByDateAsync(request.Date, cancellationToken);
        if (foodLogs.Count == 0) return [];

        var energyLogIds = foodLogs.Select(fl => fl.EnergyLogId).ToList();
        var energyLogs   = await energyRepo.GetManyByIdsAsync(energyLogIds, cancellationToken);
        var caloriesById = energyLogs.ToDictionary(e => e.Id, e => e.Calories);

        return foodLogs
            .Select(fl => new FoodLogEntryDto(
                fl.Id,
                fl.EnergyLogId,
                fl.FoodItem,
                fl.Quantity,
                caloriesById.TryGetValue(fl.EnergyLogId, out var cal) ? cal : 0))
            .ToList();
    }
}
