using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetRoutineItems;

public record GetRoutineItemsQuery : IRequest<IReadOnlyList<RoutineItemDto>>;
