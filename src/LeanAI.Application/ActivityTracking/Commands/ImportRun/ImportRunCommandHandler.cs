using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.ActivityTracking.Services;
using LeanAI.Application.WeightManagement.Commands.AppendActivityComment;
using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.ImportRun;

public sealed class ImportRunCommandHandler(
    IRunImageAnalysisService analysisService,
    IActivityLogRepository   activityRepo,
    IMediator                mediator)
    : IRequestHandler<ImportRunCommand, IReadOnlyList<ActivityMetricDto>>
{
    public async Task<IReadOnlyList<ActivityMetricDto>> Handle(
        ImportRunCommand  request,
        CancellationToken cancellationToken)
    {
        var metrics = await analysisService.AnalyzeAsync(request.ImageBytes, request.MimeType, cancellationToken);

        var logs = metrics.Select(m => new ActivityLog
        {
            Date          = request.Date,
            Activity      = "run",
            ParameterName = m.ParameterName,
            Value         = m.Value,
            Unit          = m.Unit
        });

        await activityRepo.AddRangeAsync(logs, cancellationToken);

        var distance = metrics.FirstOrDefault(m => m.ParameterName == "distance");
        var pace     = metrics.FirstOrDefault(m => m.ParameterName == "pace");
        var duration = metrics.FirstOrDefault(m => m.ParameterName == "duration");

        var comment =
            $"I run for {distance?.Value}{distance?.Unit} at a pace of " +
            $"{pace?.Value}{pace?.Unit}. Total time: {duration?.Value}{duration?.Unit}.";

        await mediator.Send(new AppendActivityCommentCommand(request.Date, comment), cancellationToken);

        return metrics;
    }
}
