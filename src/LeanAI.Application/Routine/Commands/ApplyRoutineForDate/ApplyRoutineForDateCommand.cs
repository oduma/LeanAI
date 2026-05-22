using MediatR;

namespace LeanAI.Application.Routine.Commands.ApplyRoutineForDate;

public record ApplyRoutineForDateCommand(DateOnly Date) : IRequest;
