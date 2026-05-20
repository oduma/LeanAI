using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetRoutineStatusForDate;

public record GetRoutineStatusForDateQuery(DateOnly Date) : IRequest<bool>;
