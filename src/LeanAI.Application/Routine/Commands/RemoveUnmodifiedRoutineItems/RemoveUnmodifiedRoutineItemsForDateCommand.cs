using MediatR;

namespace LeanAI.Application.Routine.Commands.RemoveUnmodifiedRoutineItems;

public record RemoveUnmodifiedRoutineItemsForDateCommand(DateOnly Date) : IRequest;
