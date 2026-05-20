using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.SaveRoutineFromDay;

public sealed class SaveRoutineFromDayCommandHandler(IRoutineRepository repo)
    : IRequestHandler<SaveRoutineFromDayCommand>
{
    public async Task Handle(SaveRoutineFromDayCommand request, CancellationToken cancellationToken)
    {
        var items = request.Items.Select(dto => new RoutineItem
        {
            SourceType  = dto.SourceType,
            Description = dto.Description,
            Quantity    = dto.Quantity,
            Calories    = dto.Calories
        }).ToList();

        await repo.ReplaceAllAsync(items, cancellationToken);
    }
}
