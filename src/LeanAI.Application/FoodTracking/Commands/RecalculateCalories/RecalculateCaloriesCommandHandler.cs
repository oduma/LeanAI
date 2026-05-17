using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Services;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.RecalculateCalories;

public sealed class RecalculateCaloriesCommandHandler(IFoodImageAnalysisService analysisService)
    : IRequestHandler<RecalculateCaloriesCommand, IReadOnlyList<FoodItemDto>>
{
    public Task<IReadOnlyList<FoodItemDto>> Handle(RecalculateCaloriesCommand request, CancellationToken cancellationToken)
        => analysisService.RecalculateCaloriesAsync(request.Items, cancellationToken);
}
