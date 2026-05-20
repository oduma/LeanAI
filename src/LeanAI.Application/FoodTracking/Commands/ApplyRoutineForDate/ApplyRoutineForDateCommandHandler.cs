using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.ApplyRoutineForDate;

public sealed class ApplyRoutineForDateCommandHandler(
    IRoutineRepository           routineRepo,
    IFoodLogRepository           foodRepo,
    ICaloryLogRepository         caloryRepo,
    ICustomActivityLogRepository customActivityRepo)
    : IRequestHandler<ApplyRoutineForDateCommand>
{
    public async Task Handle(ApplyRoutineForDateCommand request, CancellationToken cancellationToken)
    {
        var items = await routineRepo.GetAllAsync(cancellationToken);

        var foodItems     = items.Where(i => i.SourceType == "food").ToList();
        var activityItems = items.Where(i => i.SourceType == "activity").ToList();

        if (foodItems.Count > 0)
        {
            var foodLogs = foodItems.Select(item => new FoodLog
            {
                Date     = request.Date,
                FoodItem = item.Description,
                Quantity = item.Quantity ?? string.Empty,
                CaloryLog = new CaloryLog
                {
                    Date          = request.Date,
                    Calories      = item.Calories,
                    SourceType    = "food",
                    RoutineItemId = item.Id
                }
            });
            await foodRepo.AddRangeAsync(foodLogs, cancellationToken);
        }

        foreach (var item in activityItems)
        {
            var caloryLog = await caloryRepo.AddRoutineCopyAsync(new CaloryLog
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
                CaloryLogId = caloryLog.Id
            }, cancellationToken);
        }

        await routineRepo.SetIsActiveForDateAsync(request.Date, true, cancellationToken);
    }
}
