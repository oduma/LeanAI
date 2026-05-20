using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.ActivityTracking.Services;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.AnalyzeRunImage;

public sealed class AnalyzeRunImageCommandHandler(IRunImageAnalysisService analysisService)
    : IRequestHandler<AnalyzeRunImageCommand, RunImportResultDto>
{
    public async Task<RunImportResultDto> Handle(
        AnalyzeRunImageCommand request,
        CancellationToken      cancellationToken)
    {
        var metrics = await analysisService.AnalyzeAsync(
            request.ImageBytes, request.MimeType, cancellationToken);

        var distance          = metrics.FirstOrDefault(m => m.ParameterName == "distance");
        var pace              = metrics.FirstOrDefault(m => m.ParameterName == "pace");
        var duration          = metrics.FirstOrDefault(m => m.ParameterName == "duration");
        var caloriesBurnedRaw = metrics.FirstOrDefault(m => m.ParameterName == "calories_burned");

        var activityText =
            $"I run for {distance?.Value}{distance?.Unit} at a pace of " +
            $"{pace?.Value}{pace?.Unit}. Total time: {duration?.Value}{duration?.Unit}.";

        double.TryParse(caloriesBurnedRaw?.Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var calories);

        return new RunImportResultDto(activityText, calories, metrics);
    }
}
