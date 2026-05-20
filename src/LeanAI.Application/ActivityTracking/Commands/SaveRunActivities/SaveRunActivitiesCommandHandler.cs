using LeanAI.Application.WeightManagement.Commands.AppendActivityComment;
using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.SaveRunActivities;

public sealed class SaveRunActivitiesCommandHandler(
    ICaloryLogRepository         caloryLogRepo,
    ICustomActivityLogRepository customActivityLogRepo,
    IActivityLogRepository       activityLogRepo,
    IMediator                    mediator)
    : IRequestHandler<SaveRunActivitiesCommand>
{
    public async Task Handle(SaveRunActivitiesCommand request, CancellationToken cancellationToken)
    {
        // In edit mode replace all activity calories for the day; in import mode add alongside existing entries
        if (!request.IsImportMode)
            await caloryLogRepo.DeleteActivityCaloriesForDateAsync(request.Date, cancellationToken);

        foreach (var row in request.Rows)
        {
            var caloryLog = await caloryLogRepo.AddActivityAsync(
                request.Date, row.Calories, row.ActivityText, cancellationToken);

            if (row.IsRunRow && request.IsImportMode && row.Metrics is not null)
            {
                // Save individual run metrics to ActivityLog
                var logs = row.Metrics.Select(m => new ActivityLog
                {
                    Date          = request.Date,
                    Activity      = "run",
                    ParameterName = m.ParameterName,
                    Value         = m.Value,
                    Unit          = m.Unit
                });
                await activityLogRepo.AddRangeAsync(logs, cancellationToken);
            }
            else if (!row.IsRunRow)
            {
                // Custom activity — record in CustomActivityLog
                await customActivityLogRepo.AddAsync(new CustomActivityLog
                {
                    Date        = request.Date,
                    Description = row.ActivityText,
                    CaloryLogId = caloryLog.Id
                }, cancellationToken);
            }
        }

        // Append all activity descriptions to the day's notes as one block
        var allTexts = request.Rows
            .Select(r => r.ActivityText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (allTexts.Count > 0)
            await mediator.Send(
                new AppendActivityCommentCommand(request.Date, string.Join("\n", allTexts)),
                cancellationToken);
    }
}
