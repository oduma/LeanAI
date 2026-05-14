using FluentAssertions;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Queries.GetCalendarMonth;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GetCalendarMonthQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository>       _profileMock  = new();
    private readonly Mock<IDailyActualWeightRepository> _dailyMock    = new();
    private readonly Mock<IWeeklyAverageRepository>     _weeklyMock   = new();
    private readonly Mock<IAppSettingsRepository>       _settingsMock = new();
    private readonly GetCalendarMonthQueryHandler       _handler;

    // Test month: May 2026
    // May 1 = Friday, May 31 = Sunday
    // GoalStart = May 5 (Monday), GoalEnd = May 28 (Thursday)
    private static readonly DateOnly GoalStart = new(2026, 5, 5);
    private static readonly DateOnly GoalEnd   = new(2026, 5, 28);
    private static readonly DateOnly Today     = new(2026, 5, 13); // Wednesday

    public GetCalendarMonthQueryHandlerTests()
    {
        _handler = new GetCalendarMonthQueryHandler(
            _profileMock.Object, _dailyMock.Object, _weeklyMock.Object, _settingsMock.Object,
            () => Today);

        // Sensible defaults
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { GoalStartDate = GoalStart, GoalEndDate = GoalEnd });
        _settingsMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { CalendarFirstDay = DayOfWeek.Monday });
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _weeklyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task Handle_WhenProfileIsNull_ReturnsNull()
    {
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenGoalStartDateIsNull_ReturnsNull()
    {
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { GoalStartDate = null, GoalEndDate = GoalEnd });

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenGoalEndDateIsNull_ReturnsNull()
    {
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { GoalStartDate = GoalStart, GoalEndDate = null });

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_GridCellCount_IsMultipleOfSeven()
    {
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result.Should().NotBeNull();
        (result!.Days.Count % 7).Should().Be(0);
    }

    [Fact]
    public async Task Handle_MondayStart_GridStartsOnMondayBeforeFirstOfMonth()
    {
        // May 1 2026 is a Friday → grid should start Mon Apr 27
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days[0].Date.Should().Be(new DateOnly(2026, 4, 27));
    }

    [Fact]
    public async Task Handle_SundayStart_GridStartsOnSundayBeforeFirstOfMonth()
    {
        _settingsMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { CalendarFirstDay = DayOfWeek.Sunday });

        // May 1 2026 is a Friday → nearest Sunday before = Apr 26
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days[0].Date.Should().Be(new DateOnly(2026, 4, 26));
        result.FirstDayOfWeek.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public async Task Handle_DaysBeforeGoalStart_AreOutOfRange()
    {
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        // May 1–4 are before GoalStart (May 5)
        result!.Days.Where(d => d.Date >= new DateOnly(2026, 5, 1) && d.Date < GoalStart)
               .Should().AllSatisfy(d => d.State.Should().Be(CalendarDayState.OutOfRange));
    }

    [Fact]
    public async Task Handle_DaysAfterGoalEnd_AreOutOfRange()
    {
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Where(d => d.Date > GoalEnd && d.Date.Month == 5)
               .Should().AllSatisfy(d => d.State.Should().Be(CalendarDayState.OutOfRange));
    }

    [Fact]
    public async Task Handle_FutureDatesWithinGoal_AreFutureInRange()
    {
        // Today = May 13. May 14–28 are in-range future.
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days
            .Where(d => d.Date > Today && d.Date <= GoalEnd)
            .Should().AllSatisfy(d => d.State.Should().Be(CalendarDayState.FutureInRange));
    }

    [Fact]
    public async Task Handle_PastInRangeDateWithNoRecord_IsPastNoRecord()
    {
        // May 12 is before Today, within goal, no actual weight
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        var may12 = result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 12));
        may12.State.Should().Be(CalendarDayState.PastNoRecord);
        may12.Halo.Should().Be(CalendarHaloColor.None);
    }

    [Fact]
    public async Task Handle_TodayWithRecord_IsPastHasRecord()
    {
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyActualWeight { Date = Today, WeightKg = 80.0 }]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        var today = result!.Days.Single(d => d.Date == Today);
        today.State.Should().Be(CalendarDayState.PastHasRecord);
        today.WeightKg.Should().Be(80.0);
    }

    [Fact]
    public async Task Handle_FirstRecordedDay_GetsCopperHalo()
    {
        // Only one entry — no previous day to compare
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyActualWeight { Date = new DateOnly(2026, 5, 7), WeightKg = 80.0 }]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        var day = result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 7));
        day.Halo.Should().Be(CalendarHaloColor.Copper);
    }

    [Fact]
    public async Task Handle_WeightLossDay_GetsCopperHalo()
    {
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DailyActualWeight { Date = new DateOnly(2026, 5, 7), WeightKg = 82.0 },
                new DailyActualWeight { Date = new DateOnly(2026, 5, 8), WeightKg = 80.0 },
            ]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 8)).Halo.Should().Be(CalendarHaloColor.Copper);
    }

    [Fact]
    public async Task Handle_WeightGainDay_GetsNickelHalo()
    {
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DailyActualWeight { Date = new DateOnly(2026, 5, 7), WeightKg = 80.0 },
                new DailyActualWeight { Date = new DateOnly(2026, 5, 8), WeightKg = 82.0 },
            ]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 8)).Halo.Should().Be(CalendarHaloColor.Nickel);
    }

    [Fact]
    public async Task Handle_SameWeightDay_GetsNickelHalo()
    {
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DailyActualWeight { Date = new DateOnly(2026, 5, 7), WeightKg = 80.0 },
                new DailyActualWeight { Date = new DateOnly(2026, 5, 8), WeightKg = 80.0 },
            ]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 8)).Halo.Should().Be(CalendarHaloColor.Nickel);
    }

    [Fact]
    public async Task Handle_WeekAvgLowerThanPrevWeek_GetsCopperTrend()
    {
        // Week of May 4 (Mon 4 – Sun 10) avg = 82, week of May 11 avg = 80 → copper
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyActualWeight { Date = new DateOnly(2026, 5, 12), WeightKg = 80.0 }]);
        _weeklyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WeeklyAverage { WeekStart = new DateOnly(2026, 5, 4),  AverageWeightKg = 82.0 },
                new WeeklyAverage { WeekStart = new DateOnly(2026, 5, 11), AverageWeightKg = 80.0 },
            ]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 12)).TextTrend.Should().Be(CalendarTrendColor.Copper);
    }

    [Fact]
    public async Task Handle_WeekAvgHigherThanPrevWeek_GetsNickelTrend()
    {
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyActualWeight { Date = new DateOnly(2026, 5, 12), WeightKg = 84.0 }]);
        _weeklyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WeeklyAverage { WeekStart = new DateOnly(2026, 5, 4),  AverageWeightKg = 80.0 },
                new WeeklyAverage { WeekStart = new DateOnly(2026, 5, 11), AverageWeightKg = 84.0 },
            ]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 12)).TextTrend.Should().Be(CalendarTrendColor.Nickel);
    }

    [Fact]
    public async Task Handle_FirstWeekWithRecord_HasNoneTextTrend()
    {
        // Only one weekly average — no previous week to compare
        _dailyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DailyActualWeight { Date = new DateOnly(2026, 5, 7), WeightKg = 80.0 }]);
        _weeklyMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WeeklyAverage { WeekStart = new DateOnly(2026, 5, 4), AverageWeightKg = 80.0 }]);

        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Single(d => d.Date == new DateOnly(2026, 5, 7)).TextTrend.Should().Be(CalendarTrendColor.None);
    }

    [Fact]
    public async Task Handle_OutOfRangeDays_HaveNoHaloAndNoTrend()
    {
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Where(d => d.State == CalendarDayState.OutOfRange)
               .Should().AllSatisfy(d =>
               {
                   d.Halo.Should().Be(CalendarHaloColor.None);
                   d.TextTrend.Should().Be(CalendarTrendColor.None);
               });
    }

    [Fact]
    public async Task Handle_AdjacentMonthDaysInGrid_AreMarkedNotInDisplayedMonth()
    {
        // May 2026 with Monday start: grid starts Apr 27
        var result = await _handler.Handle(new GetCalendarMonthQuery(2026, 5), CancellationToken.None);

        result!.Days.Where(d => d.Date.Month != 5)
               .Should().AllSatisfy(d => d.IsInDisplayedMonth.Should().BeFalse());
        result.Days.Where(d => d.Date.Month == 5)
               .Should().AllSatisfy(d => d.IsInDisplayedMonth.Should().BeTrue());
    }
}
