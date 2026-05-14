using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.UpsertDailyLog;

public sealed class UpsertDailyLogCommandHandler(
    IDailyActualWeightRepository dailyRepo,
    IWeeklyAverageRepository     weeklyRepo)
    : IRequestHandler<UpsertDailyLogCommand>
{
    public async Task Handle(UpsertDailyLogCommand request, CancellationToken cancellationToken)
    {
        var entry = await dailyRepo.GetByDateAsync(request.Date, cancellationToken);

        if (entry is null)
        {
            entry = new DailyActualWeight
            {
                Date     = request.Date,
                WeightKg = request.WeightKg,
                Notes    = request.Notes
            };
        }
        else
        {
            entry.WeightKg = request.WeightKg;
            entry.Notes    = request.Notes;
        }

        await dailyRepo.UpsertAsync(entry, cancellationToken);

        // Keep the weekly average table in sync after every save.
        var monday  = GetMonday(request.Date);
        var sunday  = monday.AddDays(6);
        var week    = await dailyRepo.GetRangeAsync(monday, sunday, cancellationToken);
        if (week.Count > 0)
            await weeklyRepo.UpsertAsync(
                new WeeklyAverage { WeekStart = monday, AverageWeightKg = week.Average(e => e.WeightKg) },
                cancellationToken);
    }

    private static DateOnly GetMonday(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysFromMonday);
    }
}
