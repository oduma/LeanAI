using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.ApplyRoutineForDate;

public record ApplyRoutineForDateCommand(DateOnly Date) : IRequest;
