using FluentAssertions;
using LeanAI.Application.WeightManagement.Queries.GetTrendsData;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GetTrendsDataQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository>        _profileMock = new();
    private readonly Mock<IDailyIdealWeightRepository>   _idealMock   = new();
    private readonly Mock<IDailyActualWeightRepository>  _actualMock  = new();
    private readonly GetTrendsDataQueryHandler           _handler;

    // Base date range: Mon 2026-01-05 → Mon 2026-01-26 (22 days, 4 Monday anchors)
    private static readonly DateOnly GoalStart = new(2026, 1, 5);
    private static readonly DateOnly GoalEnd   = new(2026, 1, 26);

    // 22 daily ideal weights from 84.0 down to 74.0
    private static IReadOnlyList<DailyIdealWeight> DefaultIdeal =>
        Enumerable.Range(0, 22)
            .Select(i => new DailyIdealWeight { Date = GoalStart.AddDays(i), WeightKg = 84.0 - i * (10.0 / 21) })
            .ToList();

    // Actual weights — one entry per week anchor (Mon Jan 5, 12, 19, 26)
    private static IReadOnlyList<DailyActualWeight> DefaultActual =>
    [
        new() { Date = new DateOnly(2026, 1, 5),  WeightKg = 80.0 },
        new() { Date = new DateOnly(2026, 1, 6),  WeightKg = 79.0 },
        new() { Date = new DateOnly(2026, 1, 12), WeightKg = 78.0 },
        new() { Date = new DateOnly(2026, 1, 13), WeightKg = 77.0 },
        new() { Date = new DateOnly(2026, 1, 19), WeightKg = 76.0 },
        new() { Date = new DateOnly(2026, 1, 20), WeightKg = 75.0 },
        new() { Date = new DateOnly(2026, 1, 26), WeightKg = 74.0 },
    ];

    public GetTrendsDataQueryHandlerTests()
    {
        _handler = new GetTrendsDataQueryHandler(
            _profileMock.Object,
            _idealMock.Object,
            _actualMock.Object);

        // Defaults: profile with goal dates set, full data present
        SetupProfile(GoalStart, GoalEnd);
        _idealMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(DefaultIdeal);
        _actualMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DefaultActual);
    }

    private void SetupProfile(DateOnly? start, DateOnly? end)
    {
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new UserProfile { GoalStartDate = start, GoalEndDate = end });
    }

    // ── Null / guard cases ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Null_When_ProfileIsNull()
    {
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserProfile?)null);

        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_Null_When_GoalDatesNotSet()
    {
        SetupProfile(null, null);

        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_Null_When_IdealWeightTableEmpty()
    {
        _idealMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<DailyIdealWeight>());

        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    // ── Date range ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Correct_FirstAndLastDate()
    {
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.FirstDate.Should().Be(GoalStart);
        result.LastDate.Should().Be(GoalEnd);
    }

    // ── Series ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IdealSeries_Contains_AllIdealRows()
    {
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.IdealSeries.Should().HaveCount(22);
    }

    [Fact]
    public async Task Handle_ActualSeries_Contains_OnlyRecordedDays()
    {
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.ActualSeries.Should().HaveCount(DefaultActual.Count);
    }

    // ── Weekly averages ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WeeklyAverage_IsMeanOf_MonToSun_Entries()
    {
        // Week of Jan 5: only Jan 5=80 and Jan 6=79 recorded → mean = 79.5
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        var week1Avg = result!.ActualWeeklyAverages.First(p => p.Date == new DateOnly(2026, 1, 5));
        week1Avg.WeightKg.Should().BeApproximately(79.5, 0.001);
    }

    [Fact]
    public async Task Handle_IdealWeeklyAverages_ComputedForEachAnchor()
    {
        // 4 anchors: Jan 5, 12, 19, 26 (all Mondays; Jan 26 = GoalEnd = Monday, already included)
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.IdealWeeklyAverages.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_PartialLastWeek_IsIncluded_AsAnchor()
    {
        // GoalEndDate = Friday Jan 16 → anchors: Jan 5, Jan 12, Jan 16 (GoalEnd, not Monday)
        SetupProfile(GoalStart, new DateOnly(2026, 1, 16));

        var partial = Enumerable.Range(0, 12)
            .Select(i => new DailyIdealWeight { Date = GoalStart.AddDays(i), WeightKg = 84.0 - i })
            .ToList();
        _idealMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(partial);

        var partialActual = new List<DailyActualWeight>
        {
            new() { Date = new DateOnly(2026, 1, 12), WeightKg = 80.0 },
            new() { Date = new DateOnly(2026, 1, 14), WeightKg = 79.0 },
            new() { Date = new DateOnly(2026, 1, 16), WeightKg = 78.0 },
        };
        _actualMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(partialActual);

        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.ActualWeeklyAverages.Should().Contain(p => p.Date == new DateOnly(2026, 1, 16));
    }

    // ── Weekly deltas ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WeeklyDelta_Positive_WhenWeightDecreases()
    {
        // Week1 avg=79.5, Week2 avg=77.5 → delta = 79.5 - 77.5 = 2.0 > 0
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.WeeklyDeltas.Should().Contain(d => d.DeltaKg > 0);
    }

    [Fact]
    public async Task Handle_WeeklyDelta_Negative_WhenWeightIncreases()
    {
        // Reverse: week 2 heavier than week 1
        _actualMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<DailyActualWeight>
                   {
                       new() { Date = new DateOnly(2026, 1, 5),  WeightKg = 75.0 }, // week 1 avg = 75
                       new() { Date = new DateOnly(2026, 1, 12), WeightKg = 77.0 }, // week 2 avg = 77 (heavier)
                   });

        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        // delta = avg(week1) - avg(week2) = 75 - 77 = -2
        result!.WeeklyDeltas.Should().Contain(d => d.DeltaKg < 0);
    }

    [Fact]
    public async Task Handle_WeeklyDeltas_Count_IsOneFewerThan_ActualWeeklyAverages()
    {
        // 4 anchors all have actual data → 4 avg points → 3 deltas
        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.WeeklyDeltas.Should().HaveCount(result.ActualWeeklyAverages.Count - 1);
    }

    [Fact]
    public async Task Handle_SkipsWeek_WhenNoActualData_ForThatAnchor()
    {
        // Week 2 (Jan 12-18) has no actual recordings → ActualWeeklyAverages has no entry for Jan 12
        _actualMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<DailyActualWeight>
                   {
                       new() { Date = new DateOnly(2026, 1, 5),  WeightKg = 80.0 }, // week 1
                       // week 2 missing
                       new() { Date = new DateOnly(2026, 1, 19), WeightKg = 76.0 }, // week 3
                       new() { Date = new DateOnly(2026, 1, 26), WeightKg = 74.0 }, // week 4
                   });

        var result = await _handler.Handle(new GetTrendsDataQuery(), CancellationToken.None);

        result!.ActualWeeklyAverages.Should().NotContain(p => p.Date == new DateOnly(2026, 1, 12));
    }
}
