using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.RemoveUnmodifiedRoutineItems;

public record RemoveUnmodifiedRoutineItemsForDateCommand(DateOnly Date) : IRequest;
