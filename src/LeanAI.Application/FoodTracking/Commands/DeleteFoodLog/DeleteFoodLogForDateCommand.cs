using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.DeleteFoodLog;

public sealed record DeleteFoodLogForDateCommand(DateOnly Date) : IRequest;
