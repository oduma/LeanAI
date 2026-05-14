using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetLogContext;

public sealed class GetLogContextQueryHandler(
    IUserProfileRepository       profileRepository,
    IDailyActualWeightRepository actualRepository,
    IDailyIdealWeightRepository  idealRepository)
    : IRequestHandler<GetLogContextQuery, LogContextDto>
{
    public async Task<LogContextDto> Handle(GetLogContextQuery request, CancellationToken cancellationToken)
    {
        var date = request.Date;

        var profile   = await profileRepository.GetAsync(cancellationToken);
        var today     = await actualRepository.GetByDateAsync(date, cancellationToken);
        var yesterday = await actualRepository.GetByDateAsync(date.AddDays(-1), cancellationToken);
        var ideal     = await idealRepository.GetByDateAsync(date, cancellationToken);

        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var weekStart       = date.AddDays(-daysSinceMonday);
        var weekEnd         = weekStart.AddDays(6);
        var weekEntries     = await actualRepository.GetRangeAsync(weekStart, weekEnd, cancellationToken);

        var weekOrdered       = weekEntries.OrderBy(e => e.Date).ToList();
        var weekFirstWeightKg = weekOrdered.Count > 0 ? weekOrdered[0].WeightKg : (double?)null;

        var lastWeekStart  = weekStart.AddDays(-7);
        var lastWeekEnd    = weekStart.AddDays(-1);
        var lastWeekEntries = await actualRepository.GetRangeAsync(lastWeekStart, lastWeekEnd, cancellationToken);

        double? currentWeekAvg = weekOrdered.Count > 0
            ? weekOrdered.Average(e => e.WeightKg)
            : null;
        double? lastWeekAvg = lastWeekEntries.Any()
            ? lastWeekEntries.Average(e => e.WeightKg)
            : null;

        double? idealWeeklyLoss = null;
        if (profile?.StartingWeightKg.HasValue == true
            && profile.TargetWeightKg.HasValue
            && profile.TargetPeriod.HasValue)
        {
            idealWeeklyLoss = (profile.StartingWeightKg.Value - profile.TargetWeightKg.Value)
                              / profile.TargetPeriod.Value.TotalDays() * 7.0;
        }

        return new LogContextDto(
            TodayWeightKg:              today?.WeightKg,
            TodayNotes:                 today?.Notes,
            YesterdayWeightKg:          yesterday?.WeightKg,
            TodayIdealWeightKg:         ideal?.WeightKg,
            WeekFirstWeightKg:          weekFirstWeightKg,
            WeekDaysLogged:             weekOrdered.Count,
            IdealWeeklyLossKg:          idealWeeklyLoss,
            UnitSystem:                 profile?.UnitSystem ?? UnitSystem.Metric,
            WeekStartDate:              weekStart,
            CurrentWeekAverageWeightKg: currentWeekAvg,
            LastWeekAverageWeightKg:    lastWeekAvg
        );
    }
}
