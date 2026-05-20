using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.SaveFoodLog;

public sealed class SaveFoodLogCommandHandler(IFoodLogRepository repo)
    : IRequestHandler<SaveFoodLogCommand>
{
    public async Task Handle(SaveFoodLogCommand request, CancellationToken cancellationToken)
    {
        // In edit mode replace all food for the day; in import mode add alongside existing meals
        if (!request.IsImportMode)
            await repo.DeleteByDateAsync(request.Date, cancellationToken);

        var entities = request.Items.Select(item => new FoodLog
        {
            Date      = request.Date,
            FoodItem  = item.FoodItem,
            Quantity  = item.Quantity,
            CaloryLog = new CaloryLog
            {
                Date       = request.Date,
                Calories   = item.Calories,
                SourceType = "food"
            }
        });

        await repo.AddRangeAsync(entities, cancellationToken);
    }
}
