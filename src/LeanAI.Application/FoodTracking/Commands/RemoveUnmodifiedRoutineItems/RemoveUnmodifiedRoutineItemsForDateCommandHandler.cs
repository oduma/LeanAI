using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.RemoveUnmodifiedRoutineItems;

public sealed class RemoveUnmodifiedRoutineItemsForDateCommandHandler(
    IRoutineRepository   routineRepo,
    ICaloryLogRepository caloryRepo,
    IFoodLogRepository   foodRepo)
    : IRequestHandler<RemoveUnmodifiedRoutineItemsForDateCommand>
{
    public async Task Handle(RemoveUnmodifiedRoutineItemsForDateCommand request, CancellationToken cancellationToken)
    {
        var copies      = await caloryRepo.GetRoutineCopiesForDateAsync(request.Date, cancellationToken);
        var allRoutines = await routineRepo.GetAllAsync(cancellationToken);
        var routineById = allRoutines.ToDictionary(r => r.Id);

        foreach (var copy in copies)
        {
            if (copy.RoutineItemId is null || !routineById.TryGetValue(copy.RoutineItemId.Value, out var routine))
                continue;

            bool isUnmodified;

            if (copy.SourceType == "food")
            {
                var foodLog = await foodRepo.GetByCaloryLogIdAsync(copy.Id, cancellationToken);
                if (foodLog is null)
                {
                    isUnmodified = false;
                }
                else
                {
                    isUnmodified = copy.Calories      == routine.Calories
                                && foodLog.FoodItem   == routine.Description
                                && foodLog.Quantity   == (routine.Quantity ?? string.Empty);
                }
            }
            else // activity
            {
                isUnmodified = copy.Calories    == routine.Calories
                            && copy.Description == routine.Description;
            }

            if (isUnmodified)
                await caloryRepo.DeleteRoutineCopyAsync(copy, cancellationToken);
        }

        await routineRepo.SetIsActiveForDateAsync(request.Date, false, cancellationToken);
    }
}
