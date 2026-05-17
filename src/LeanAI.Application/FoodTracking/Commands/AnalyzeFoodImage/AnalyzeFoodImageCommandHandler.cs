using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Services;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.AnalyzeFoodImage;

public sealed class AnalyzeFoodImageCommandHandler(IFoodImageAnalysisService analysisService)
    : IRequestHandler<AnalyzeFoodImageCommand, IReadOnlyList<FoodItemDto>>
{
    public Task<IReadOnlyList<FoodItemDto>> Handle(AnalyzeFoodImageCommand request, CancellationToken cancellationToken)
        => analysisService.AnalyzeImageAsync(request.ImageBytes, request.MimeType, cancellationToken);
}
