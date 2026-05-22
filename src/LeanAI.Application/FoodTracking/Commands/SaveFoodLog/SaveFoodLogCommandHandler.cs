using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.SaveFoodLog;

public sealed class SaveFoodLogCommandHandler(IFoodLogRepository foodRepo, IEnergyLogRepository energyRepo)
    : IRequestHandler<SaveFoodLogCommand>
{
    public async Task Handle(SaveFoodLogCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsImportMode)
        {
            // Explicit cascade: delete EnergyLogs linked to existing FoodLogs first
            var existing  = await foodRepo.GetByDateAsync(request.Date, cancellationToken);
            var energyIds = existing.Select(fl => fl.EnergyLogId).ToList();
            await foodRepo.DeleteByDateAsync(request.Date, cancellationToken);
            await energyRepo.DeleteManyAsync(energyIds, cancellationToken);
        }

        var foodLogs = new List<FoodLog>();
        foreach (var item in request.Items)
        {
            var energyLog = await energyRepo.AddAsync(new EnergyLog
            {
                Date       = request.Date,
                Calories   = item.Calories,
                SourceType = "food"
            }, cancellationToken);

            foodLogs.Add(new FoodLog
            {
                Date        = request.Date,
                FoodItem    = item.FoodItem,
                Quantity    = item.Quantity,
                EnergyLogId = energyLog.Id
            });
        }

        await foodRepo.AddRangeAsync(foodLogs, cancellationToken);
    }
}
