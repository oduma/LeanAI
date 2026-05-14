using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetCalendarMonth;

public sealed class GetCalendarMonthQueryHandler : IRequestHandler<GetCalendarMonthQuery, CalendarMonthDto?>
{
    private readonly IUserProfileRepository       _profileRepo;
    private readonly IDailyActualWeightRepository _dailyRepo;
    private readonly IWeeklyAverageRepository     _weeklyRepo;
    private readonly IAppSettingsRepository       _settingsRepo;
    private readonly Func<DateOnly>               _today;

    public GetCalendarMonthQueryHandler(
        IUserProfileRepository       profileRepo,
        IDailyActualWeightRepository dailyRepo,
        IWeeklyAverageRepository     weeklyRepo,
        IAppSettingsRepository       settingsRepo,
        Func<DateOnly>?              today = null)
    {
        _profileRepo  = profileRepo;
        _dailyRepo    = dailyRepo;
        _weeklyRepo   = weeklyRepo;
        _settingsRepo = settingsRepo;
        _today        = today ?? (() => DateOnly.FromDateTime(DateTime.Today));
    }

    public async Task<CalendarMonthDto?> Handle(GetCalendarMonthQuery request, CancellationToken ct)
    {
        var profile = await _profileRepo.GetAsync(ct);
        if (profile?.GoalStartDate is null || profile.GoalEndDate is null)
            return null;

        var goalStart = profile.GoalStartDate.Value;
        var goalEnd   = profile.GoalEndDate.Value;
        var today     = _today();

        var settings       = await _settingsRepo.GetAsync(ct);
        var calendarFirst  = settings?.CalendarFirstDay ?? DayOfWeek.Monday;

        // Grid bounds: expand the displayed month to complete weeks.
        var firstOfMonth = new DateOnly(request.Year, request.Month, 1);
        var lastOfMonth  = new DateOnly(request.Year, request.Month,
                               DateTime.DaysInMonth(request.Year, request.Month));

        var gridStart = StartOfWeek(firstOfMonth, calendarFirst);
        var gridEnd   = EndOfWeek(lastOfMonth, calendarFirst);

        // Load data for the entire grid range.
        var actualWeights = await _dailyRepo.GetRangeAsync(gridStart, gridEnd, ct);
        var actualByDate  = actualWeights.ToDictionary(e => e.Date, e => e.WeightKg);

        // Weekly averages: load for all Mon-anchored weeks visible in the grid.
        var firstMonday = GetMonday(gridStart);
        var lastMonday  = GetMonday(gridEnd);
        var weeklyAvgs  = await _weeklyRepo.GetRangeAsync(firstMonday, lastMonday, ct);
        var avgByMonday = weeklyAvgs.ToDictionary(e => e.WeekStart, e => e.AverageWeightKg);

        // Build previous-weight lookup (sorted actual entries across goal range).
        var sortedActual = actualWeights.OrderBy(e => e.Date).ToList();
        var prevWeight   = BuildPreviousWeightMap(sortedActual);

        var days = new List<CalendarDayDto>();
        for (var date = gridStart; date <= gridEnd; date = date.AddDays(1))
        {
            var state     = DetermineState(date, goalStart, goalEnd, today, actualByDate);
            var halo      = DetermineHalo(date, state, actualByDate, prevWeight);
            var trend     = DetermineTrend(date, state, avgByMonday);
            var weightKg  = actualByDate.TryGetValue(date, out var w) ? w : (double?)null;
            var inMonth   = date.Month == request.Month;

            days.Add(new CalendarDayDto(date, inMonth, state, halo, trend, weightKg));
        }

        return new CalendarMonthDto(request.Year, request.Month, goalStart, goalEnd, calendarFirst, days);
    }

    private static CalendarDayState DetermineState(
        DateOnly date, DateOnly goalStart, DateOnly goalEnd, DateOnly today,
        Dictionary<DateOnly, double> actualByDate)
    {
        if (date < goalStart || date > goalEnd) return CalendarDayState.OutOfRange;
        if (date > today)                        return CalendarDayState.FutureInRange;
        if (!actualByDate.ContainsKey(date))     return CalendarDayState.PastNoRecord;
        return CalendarDayState.PastHasRecord;
    }

    private static CalendarHaloColor DetermineHalo(
        DateOnly date, CalendarDayState state,
        Dictionary<DateOnly, double> actualByDate,
        Dictionary<DateOnly, double?> prevWeight)
    {
        if (state != CalendarDayState.PastHasRecord) return CalendarHaloColor.None;

        var current  = actualByDate[date];
        var previous = prevWeight.TryGetValue(date, out var p) ? p : null;

        if (previous is null)                 return CalendarHaloColor.Copper; // First ever — optimistic (D3)
        return current < previous.Value ? CalendarHaloColor.Copper : CalendarHaloColor.Nickel;
    }

    private static CalendarTrendColor DetermineTrend(
        DateOnly date, CalendarDayState state,
        Dictionary<DateOnly, double> avgByMonday)
    {
        if (state != CalendarDayState.PastHasRecord) return CalendarTrendColor.None;

        var monday     = GetMonday(date);
        var prevMonday = monday.AddDays(-7);

        if (!avgByMonday.TryGetValue(monday,     out var thisAvg))  return CalendarTrendColor.None;
        if (!avgByMonday.TryGetValue(prevMonday, out var prevAvg))  return CalendarTrendColor.None;

        return thisAvg < prevAvg ? CalendarTrendColor.Copper : CalendarTrendColor.Nickel;
    }

    private static Dictionary<DateOnly, double?> BuildPreviousWeightMap(
        List<DailyActualWeight> sorted)
    {
        var map = new Dictionary<DateOnly, double?>();
        double? last = null;
        foreach (var entry in sorted)
        {
            map[entry.Date] = last;
            last = entry.WeightKg;
        }
        return map;
    }

    private static DateOnly StartOfWeek(DateOnly date, DayOfWeek firstDay)
    {
        var diff = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        return date.AddDays(-diff);
    }

    private static DateOnly EndOfWeek(DateOnly date, DayOfWeek firstDay)
    {
        var lastDay  = (DayOfWeek)(((int)firstDay + 6) % 7);
        var diff     = ((int)lastDay - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(diff);
    }

    internal static DateOnly GetMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}
