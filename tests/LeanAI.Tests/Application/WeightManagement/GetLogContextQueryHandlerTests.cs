using FluentAssertions;
using LeanAI.Application.WeightManagement.Queries.GetLogContext;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GetLogContextQueryHandlerTests
{
    private readonly Mock<IUserProfileRepository>        _profileRepoMock = new();
    private readonly Mock<IDailyActualWeightRepository>  _actualRepoMock  = new();
    private readonly Mock<IDailyIdealWeightRepository>   _idealRepoMock   = new();
    private readonly GetLogContextQueryHandler           _handler;

    // Wednesday 2026-05-13 → ISO week Mon 2026-05-11 – Sun 2026-05-17
    private static readonly DateOnly TestDate  = new(2026, 5, 13);
    private static readonly DateOnly WeekStart = new(2026, 5, 11);
    private static readonly DateOnly WeekEnd   = new(2026, 5, 17);

    public GetLogContextQueryHandlerTests()
    {
        _handler = new GetLogContextQueryHandler(
            _profileRepoMock.Object,
            _actualRepoMock.Object,
            _idealRepoMock.Object);

        _profileRepoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync((UserProfile?)null);
        _actualRepoMock.Setup(r => r.GetByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync((DailyActualWeight?)null);
        _actualRepoMock.Setup(r => r.GetRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<DailyActualWeight>());
        _idealRepoMock.Setup(r => r.GetByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync((DailyIdealWeight?)null);
    }

    [Fact]
    public async Task Handle_WhenTodayEntryExists_ReturnsTodayWeightAndNotes()
    {
        var entry = new DailyActualWeight { Date = TestDate, WeightKg = 82.3, Notes = "Test notes" };
        _actualRepoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(entry);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.TodayWeightKg.Should().Be(82.3);
        result.TodayNotes.Should().Be("Test notes");
    }

    [Fact]
    public async Task Handle_WhenNoTodayEntry_ReturnsTodayWeightNull()
    {
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.TodayWeightKg.Should().BeNull();
        result.TodayNotes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenYesterdayEntryExists_ReturnsYesterdayWeight()
    {
        var yesterday = TestDate.AddDays(-1);
        var entry = new DailyActualWeight { Date = yesterday, WeightKg = 81.9 };
        _actualRepoMock.Setup(r => r.GetByDateAsync(yesterday, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(entry);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.YesterdayWeightKg.Should().Be(81.9);
    }

    [Fact]
    public async Task Handle_WhenNoYesterdayEntry_ReturnsYesterdayWeightNull()
    {
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.YesterdayWeightKg.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTodayIdealExists_ReturnsTodayIdealWeight()
    {
        var ideal = new DailyIdealWeight { Date = TestDate, WeightKg = 82.0 };
        _idealRepoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(ideal);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.TodayIdealWeightKg.Should().Be(82.0);
    }

    [Fact]
    public async Task Handle_WhenNoTodayIdeal_ReturnsTodayIdealNull()
    {
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.TodayIdealWeightKg.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenWeekEntriesExist_ReturnsWeekFirstWeightAndDaysLogged()
    {
        var entries = new List<DailyActualWeight>
        {
            new() { Date = WeekStart,              WeightKg = 83.0 },
            new() { Date = WeekStart.AddDays(1),   WeightKg = 82.7 },
            new() { Date = TestDate,               WeightKg = 82.3 }
        };
        _actualRepoMock.Setup(r => r.GetRangeAsync(WeekStart, WeekEnd, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(entries);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.WeekFirstWeightKg.Should().Be(83.0);
        result.WeekDaysLogged.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenNoWeekEntries_ReturnsWeekFirstWeightNullAndZeroDays()
    {
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.WeekFirstWeightKg.Should().BeNull();
        result.WeekDaysLogged.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenProfileComplete_ReturnsIdealWeeklyLossKg()
    {
        var profile = new UserProfile
        {
            StartingWeightKg = 90.0,
            TargetWeightKg   = 75.0,
            TargetPeriod     = TargetPeriod.ThreeMonths,
            UnitSystem       = UnitSystem.Metric
        };
        _profileRepoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(profile);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        // (90 - 75) / 90 * 7 ≈ 1.1667
        var expected = (90.0 - 75.0) / 90.0 * 7.0;
        result.IdealWeeklyLossKg.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public async Task Handle_WhenProfileMissing_ReturnsIdealWeeklyLossKgNull()
    {
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.IdealWeeklyLossKg.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenProfileExists_ReturnsItsUnitSystem()
    {
        var profile = new UserProfile { UnitSystem = UnitSystem.Imperial };
        _profileRepoMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(profile);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.UnitSystem.Should().Be(UnitSystem.Imperial);
    }

    [Fact]
    public async Task Handle_WhenNoProfile_ReturnsDefaultUnitSystem()
    {
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.UnitSystem.Should().Be(UnitSystem.Metric);
    }

    [Fact]
    public async Task Handle_ReturnsWeekStartDate_AsMonday()
    {
        // TestDate is Wed 2026-05-13 → Monday of that week is 2026-05-11
        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.WeekStartDate.Should().Be(WeekStart);
        result.WeekStartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Handle_WhenWeekEntriesExist_ReturnsCurrentWeekAverage()
    {
        var entries = new List<DailyActualWeight>
        {
            new() { Date = WeekStart,            WeightKg = 84.0 },
            new() { Date = WeekStart.AddDays(1), WeightKg = 82.0 },
            new() { Date = TestDate,             WeightKg = 80.0 }
        };
        _actualRepoMock
            .Setup(r => r.GetRangeAsync(WeekStart, WeekEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.CurrentWeekAverageWeightKg.Should().BeApproximately((84.0 + 82.0 + 80.0) / 3, 0.001);
    }

    [Fact]
    public async Task Handle_WhenLastWeekEntriesExist_ReturnsLastWeekAverage()
    {
        // Last week: Mon 2026-05-04 – Sun 2026-05-10
        var lastWeekStart = new DateOnly(2026, 5, 4);
        var lastWeekEnd   = new DateOnly(2026, 5, 10);
        var lastWeekEntries = new List<DailyActualWeight>
        {
            new() { Date = lastWeekStart,            WeightKg = 86.0 },
            new() { Date = lastWeekStart.AddDays(2), WeightKg = 85.0 }
        };
        _actualRepoMock
            .Setup(r => r.GetRangeAsync(lastWeekStart, lastWeekEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastWeekEntries);

        var result = await _handler.Handle(new GetLogContextQuery(TestDate), CancellationToken.None);

        result.LastWeekAverageWeightKg.Should().BeApproximately((86.0 + 85.0) / 2, 0.001);
    }
}
