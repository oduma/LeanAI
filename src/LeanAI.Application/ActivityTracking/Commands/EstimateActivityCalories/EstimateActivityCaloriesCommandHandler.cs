using LeanAI.Application.ActivityTracking.Services;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.EstimateActivityCalories;

public sealed class EstimateActivityCaloriesCommandHandler(IActivityCaloriesEstimationService estimationService)
    : IRequestHandler<EstimateActivityCaloriesCommand, IReadOnlyList<double>>
{
    public Task<IReadOnlyList<double>> Handle(
        EstimateActivityCaloriesCommand request,
        CancellationToken               cancellationToken)
        => estimationService.EstimateAsync(request.Descriptions, cancellationToken);
}
