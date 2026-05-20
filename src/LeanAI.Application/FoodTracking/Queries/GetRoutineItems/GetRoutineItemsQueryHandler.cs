using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetRoutineItems;

public sealed class GetRoutineItemsQueryHandler(IRoutineRepository repo)
    : IRequestHandler<GetRoutineItemsQuery, IReadOnlyList<RoutineItemDto>>
{
    public async Task<IReadOnlyList<RoutineItemDto>> Handle(GetRoutineItemsQuery request, CancellationToken cancellationToken)
    {
        var items = await repo.GetAllAsync(cancellationToken);
        return items.Select(i => new RoutineItemDto(i.Id, i.SourceType, i.Description, i.Quantity, i.Calories))
                    .ToList();
    }
}
