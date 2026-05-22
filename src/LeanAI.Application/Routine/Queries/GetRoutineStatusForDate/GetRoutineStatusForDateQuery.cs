using MediatR;

namespace LeanAI.Application.Routine.Queries.GetRoutineStatusForDate;

public record GetRoutineStatusForDateQuery(DateOnly Date) : IRequest<bool>;
