using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.RecalculateWeeklyAverage;

public sealed class RecalculateWeeklyAverageCommandHandler(
    IDailyActualWeightRepository dailyRepo,
    IWeeklyAverageRepository     weeklyRepo)
    : IRequestHandler<RecalculateWeeklyAverageCommand, Unit>
{
    public async Task<Unit> Handle(RecalculateWeeklyAverageCommand request, CancellationToken ct)
    {
        var monday = GetMonday(request.Date);
        var sunday = monday.AddDays(6);

        var entries = await dailyRepo.GetRangeAsync(monday, sunday, ct);
        if (entries.Count == 0)
            return Unit.Value;

        var avg = entries.Average(e => e.WeightKg);
        await weeklyRepo.UpsertAsync(
            new WeeklyAverage { WeekStart = monday, AverageWeightKg = avg }, ct);

        return Unit.Value;
    }

    internal static DateOnly GetMonday(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysFromMonday);
    }
}
