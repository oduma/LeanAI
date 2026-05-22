using LeanAI.Domain.Routine.Entities;
using LeanAI.Domain.Routine.Interfaces;
using MediatR;

namespace LeanAI.Application.Routine.Commands.SaveRoutineFromDay;

public sealed class SaveRoutineFromDayCommandHandler(IRoutineRepository repo)
    : IRequestHandler<SaveRoutineFromDayCommand>
{
    public async Task Handle(SaveRoutineFromDayCommand request, CancellationToken cancellationToken)
    {
        var foodItems = request.FoodItems.Select(dto => new RoutineFoodItem
        {
            Description = dto.Description,
            Quantity    = dto.Quantity,
            Calories    = dto.Calories
        }).ToList();

        var activityItems = request.ActivityItems.Select(dto => new RoutineActivityItem
        {
            Description = dto.Description,
            Calories    = dto.Calories
        }).ToList();

        await repo.ReplaceAllAsync(foodItems, activityItems, cancellationToken);
    }
}
