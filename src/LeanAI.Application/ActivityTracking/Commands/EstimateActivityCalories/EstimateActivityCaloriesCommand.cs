using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.EstimateActivityCalories;

public sealed record EstimateActivityCaloriesCommand(IReadOnlyList<string> Descriptions)
    : IRequest<IReadOnlyList<double>>;
