using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetTrendsData;

public sealed class GetTrendsDataQueryHandler(
    IUserProfileRepository       profileRepo,
    IDailyIdealWeightRepository  idealRepo,
    IDailyActualWeightRepository actualRepo)
    : IRequestHandler<GetTrendsDataQuery, TrendsDataDto?>
{
    public async Task<TrendsDataDto?> Handle(GetTrendsDataQuery request, CancellationToken ct)
    {
        var profile = await profileRepo.GetAsync(ct);
        if (profile?.GoalStartDate is null || profile.GoalEndDate is null)
            return null;

        var firstDate = profile.GoalStartDate.Value;
        var lastDate  = profile.GoalEndDate.Value;

        var idealWeights  = await idealRepo.GetRangeAsync(firstDate, lastDate, ct);
        if (idealWeights.Count == 0) return null;

        var actualWeights = await actualRepo.GetRangeAsync(firstDate, lastDate, ct);

        var idealSeries  = idealWeights.Select(e => new WeightPointDto(e.Date, e.WeightKg)).ToList();
        var actualSeries = actualWeights.Select(e => new WeightPointDto(e.Date, e.WeightKg)).ToList();

        var anchors = BuildAnchorDates(firstDate, lastDate);

        var idealByDate  = idealWeights.ToDictionary(e => e.Date, e => e.WeightKg);
        var actualByDate = actualWeights.ToDictionary(e => e.Date, e => e.WeightKg);

        var idealWeeklyAvgs  = new List<WeightPointDto>();
        var actualWeeklyAvgs = new List<WeightPointDto>();

        foreach (var anchor in anchors)
        {
            // Determine the preceding Monday for this anchor (for GoalEndDate non-Monday anchors)
            var windowStart = anchor.DayOfWeek == DayOfWeek.Monday
                ? anchor
                : anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7)); // roll back to Monday
            var windowEnd = anchor.DayOfWeek == DayOfWeek.Monday
                ? DateOnly.FromDayNumber(Math.Min(anchor.AddDays(6).DayNumber, lastDate.DayNumber))
                : anchor;

            var idealInWindow = Enumerable.Range(0, windowEnd.DayNumber - windowStart.DayNumber + 1)
                .Select(i => windowStart.AddDays(i))
                .Where(d => idealByDate.ContainsKey(d))
                .Select(d => idealByDate[d])
                .ToList();

            if (idealInWindow.Count > 0)
                idealWeeklyAvgs.Add(new WeightPointDto(anchor, idealInWindow.Average()));

            var actualInWindow = Enumerable.Range(0, windowEnd.DayNumber - windowStart.DayNumber + 1)
                .Select(i => windowStart.AddDays(i))
                .Where(d => actualByDate.ContainsKey(d))
                .Select(d => actualByDate[d])
                .ToList();

            if (actualInWindow.Count > 0)
                actualWeeklyAvgs.Add(new WeightPointDto(anchor, actualInWindow.Average()));
        }

        var weeklyDeltas = new List<WeeklyDeltaDto>();
        for (var i = 1; i < actualWeeklyAvgs.Count; i++)
        {
            var delta = actualWeeklyAvgs[i - 1].WeightKg - actualWeeklyAvgs[i].WeightKg;
            weeklyDeltas.Add(new WeeklyDeltaDto(actualWeeklyAvgs[i].Date, delta));
        }

        return new TrendsDataDto(
            firstDate,
            lastDate,
            idealSeries,
            actualSeries,
            idealWeeklyAvgs,
            actualWeeklyAvgs,
            weeklyDeltas);
    }

    private static List<DateOnly> BuildAnchorDates(DateOnly first, DateOnly last)
    {
        // Find first Monday on-or-after firstDate
        var daysToMonday = ((int)DayOfWeek.Monday - (int)first.DayOfWeek + 7) % 7;
        var current = first.AddDays(daysToMonday);

        var anchors = new List<DateOnly>();
        while (current <= last)
        {
            anchors.Add(current);
            current = current.AddDays(7);
        }

        // Append GoalEndDate if it is not already a Monday in the list
        if (last.DayOfWeek != DayOfWeek.Monday)
            anchors.Add(last);

        return anchors;
    }
}
