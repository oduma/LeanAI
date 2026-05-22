using LeanAI.Application.Routine.DTOs;
using MediatR;

namespace LeanAI.Application.Routine.Queries.GetRoutineItems;

public record GetRoutineItemsQuery : IRequest<RoutineItemsResultDto>;
