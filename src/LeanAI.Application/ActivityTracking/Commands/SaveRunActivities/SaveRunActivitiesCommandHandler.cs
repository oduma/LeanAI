using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.EnergyTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.SaveRunActivities;

public sealed class SaveRunActivitiesCommandHandler(
    IEnergyLogRepository         energyRepo,
    ICustomActivityLogRepository customActivityLogRepo,
    IActivityLogRepository       activityLogRepo)
    : IRequestHandler<SaveRunActivitiesCommand>
{
    public async Task Handle(SaveRunActivitiesCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsImportMode)
        {
            // Explicit cascade: delete CustomActivityLogs linked to activity EnergyLogs first
            var existingActivityLogs = await energyRepo.GetActivityCaloriesForDateAsync(request.Date, cancellationToken);
            var existingIds          = existingActivityLogs.Select(el => el.Id).ToList();
            await customActivityLogRepo.DeleteByEnergyLogIdsAsync(existingIds, cancellationToken);
            await energyRepo.DeleteActivityCaloriesForDateAsync(request.Date, cancellationToken);
        }

        foreach (var row in request.Rows)
        {
            var energyLog = await energyRepo.AddActivityAsync(
                request.Date, row.Calories, row.ActivityText, cancellationToken);

            if (row.IsRunRow && request.IsImportMode && row.Metrics is not null)
            {
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
                await customActivityLogRepo.AddAsync(new CustomActivityLog
                {
                    Date        = request.Date,
                    Description = row.ActivityText,
                    EnergyLogId = energyLog.Id
                }, cancellationToken);
            }
        }
    }
}
