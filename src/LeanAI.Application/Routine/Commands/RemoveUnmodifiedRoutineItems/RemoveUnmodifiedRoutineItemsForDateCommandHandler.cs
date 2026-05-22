using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Domain.Routine.Interfaces;
using MediatR;

namespace LeanAI.Application.Routine.Commands.RemoveUnmodifiedRoutineItems;

public sealed class RemoveUnmodifiedRoutineItemsForDateCommandHandler(
    IRoutineRepository           routineRepo,
    IEnergyLogRepository         energyRepo,
    IFoodLogRepository           foodRepo,
    ICustomActivityLogRepository customActivityRepo)
    : IRequestHandler<RemoveUnmodifiedRoutineItemsForDateCommand>
{
    public async Task Handle(RemoveUnmodifiedRoutineItemsForDateCommand request, CancellationToken cancellationToken)
    {
        var copies        = await energyRepo.GetRoutineCopiesForDateAsync(request.Date, cancellationToken);
        var foodItems     = await routineRepo.GetAllFoodItemsAsync(cancellationToken);
        var activityItems = await routineRepo.GetAllActivityItemsAsync(cancellationToken);

        var foodById     = foodItems.ToDictionary(r => r.Id);
        var activityById = activityItems.ToDictionary(r => r.Id);

        var foodEnergyIds     = new List<Guid>();
        var activityEnergyIds = new List<Guid>();

        foreach (var copy in copies)
        {
            if (copy.RoutineItemId is null) continue;

            if (copy.SourceType == "food" && foodById.TryGetValue(copy.RoutineItemId.Value, out var routineFood))
            {
                var foodLog = await foodRepo.GetByEnergyLogIdAsync(copy.Id, cancellationToken);
                if (foodLog is not null
                    && copy.Calories    == routineFood.Calories
                    && foodLog.FoodItem == routineFood.Description
                    && foodLog.Quantity == (routineFood.Quantity ?? string.Empty))
                {
                    foodEnergyIds.Add(copy.Id);
                }
            }
            else if (copy.SourceType == "activity" && activityById.TryGetValue(copy.RoutineItemId.Value, out var routineActivity))
            {
                if (copy.Calories    == routineActivity.Calories
                 && copy.Description == routineActivity.Description)
                {
                    activityEnergyIds.Add(copy.Id);
                }
            }
        }

        // Explicit cascade: delete linked rows before deleting EnergyLogs
        await foodRepo.DeleteByEnergyLogIdsAsync(foodEnergyIds, cancellationToken);
        await customActivityRepo.DeleteByEnergyLogIdsAsync(activityEnergyIds, cancellationToken);

        var allEnergyIdsToDelete = foodEnergyIds.Concat(activityEnergyIds).ToList();
        await energyRepo.DeleteManyAsync(allEnergyIdsToDelete, cancellationToken);

        await routineRepo.SetIsActiveForDateAsync(request.Date, false, cancellationToken);
    }
}
