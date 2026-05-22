using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Domain.Routine.Interfaces;
using MediatR;

namespace LeanAI.Application.Routine.Commands.ApplyRoutineForDate;

public sealed class ApplyRoutineForDateCommandHandler(
    IRoutineRepository           routineRepo,
    IFoodLogRepository           foodRepo,
    IEnergyLogRepository         energyRepo,
    ICustomActivityLogRepository customActivityRepo)
    : IRequestHandler<ApplyRoutineForDateCommand>
{
    public async Task Handle(ApplyRoutineForDateCommand request, CancellationToken cancellationToken)
    {
        var foodItems     = await routineRepo.GetAllFoodItemsAsync(cancellationToken);
        var activityItems = await routineRepo.GetAllActivityItemsAsync(cancellationToken);

        if (foodItems.Count > 0)
        {
            var energyLogs = new List<EnergyLog>();
            var foodLogs   = new List<FoodLog>();

            foreach (var item in foodItems)
            {
                var energyLog = await energyRepo.AddRoutineCopyAsync(new EnergyLog
                {
                    Date          = request.Date,
                    Calories      = item.Calories,
                    SourceType    = "food",
                    RoutineItemId = item.Id
                }, cancellationToken);

                foodLogs.Add(new FoodLog
                {
                    Date        = request.Date,
                    FoodItem    = item.Description,
                    Quantity    = item.Quantity ?? string.Empty,
                    EnergyLogId = energyLog.Id
                });
            }

            await foodRepo.AddRangeAsync(foodLogs, cancellationToken);
        }

        foreach (var item in activityItems)
        {
            var energyLog = await energyRepo.AddRoutineCopyAsync(new EnergyLog
            {
                Date          = request.Date,
                Calories      = item.Calories,
                SourceType    = "activity",
                Description   = item.Description,
                RoutineItemId = item.Id
            }, cancellationToken);

            await customActivityRepo.AddAsync(new CustomActivityLog
            {
                Date        = request.Date,
                Description = item.Description,
                EnergyLogId = energyLog.Id
            }, cancellationToken);
        }

        await routineRepo.SetIsActiveForDateAsync(request.Date, true, cancellationToken);
    }
}
