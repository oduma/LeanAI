using LeanAI.Application.Routine.DTOs;
using LeanAI.Domain.Routine.Interfaces;
using MediatR;

namespace LeanAI.Application.Routine.Queries.GetRoutineItems;

public sealed class GetRoutineItemsQueryHandler(IRoutineRepository repo)
    : IRequestHandler<GetRoutineItemsQuery, RoutineItemsResultDto>
{
    public async Task<RoutineItemsResultDto> Handle(GetRoutineItemsQuery request, CancellationToken cancellationToken)
    {
        var foodItems     = await repo.GetAllFoodItemsAsync(cancellationToken);
        var activityItems = await repo.GetAllActivityItemsAsync(cancellationToken);

        var foodDtos = foodItems
            .Select(i => new RoutineFoodItemDto(i.Id, i.Description, i.Quantity, i.Calories))
            .ToList();

        var activityDtos = activityItems
            .Select(i => new RoutineActivityItemDto(i.Id, i.Description, i.Calories))
            .ToList();

        return new RoutineItemsResultDto(foodDtos, activityDtos);
    }
}
