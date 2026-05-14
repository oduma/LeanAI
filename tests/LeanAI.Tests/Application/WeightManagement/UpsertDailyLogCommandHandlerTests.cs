using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.UpsertDailyLog;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class UpsertDailyLogCommandHandlerTests
{
    private readonly Mock<IDailyActualWeightRepository> _dailyRepoMock  = new();
    private readonly Mock<IWeeklyAverageRepository>     _weeklyRepoMock = new();
    private readonly UpsertDailyLogCommandHandler       _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 13); // Wednesday
    private static readonly DateOnly Monday   = new(2026, 5, 11);
    private static readonly DateOnly Sunday   = new(2026, 5, 17);

    public UpsertDailyLogCommandHandlerTests()
    {
        _handler = new UpsertDailyLogCommandHandler(_dailyRepoMock.Object, _weeklyRepoMock.Object);

        // Default: weekly range returns the upserted entry so weekly avg can be computed.
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupWeekRange(params double[] weights)
    {
        var entries = weights.Select((w, i) =>
            new DailyActualWeight { Date = Monday.AddDays(i), WeightKg = w }).ToList();
        _dailyRepoMock
            .Setup(r => r.GetRangeAsync(Monday, Sunday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
    }

    [Fact]
    public async Task Handle_WhenNoExistingEntry_CreatesNewEntityAndUpserts()
    {
        DailyActualWeight? saved = null;
        _dailyRepoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((DailyActualWeight?)null);
        _dailyRepoMock.Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
                 .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
                 .Returns(Task.CompletedTask);
        SetupWeekRange(82.3);

        await _handler.Handle(new UpsertDailyLogCommand(TestDate, 82.3, "Feeling good"), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Date.Should().Be(TestDate);
        saved.WeightKg.Should().Be(82.3);
        saved.Notes.Should().Be("Feeling good");
        _dailyRepoMock.Verify(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryExists_UpdatesExistingEntityAndUpserts()
    {
        var existing = new DailyActualWeight { Date = TestDate, WeightKg = 80.0, Notes = "Old notes" };
        DailyActualWeight? saved = null;
        _dailyRepoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);
        _dailyRepoMock.Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
                 .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
                 .Returns(Task.CompletedTask);
        SetupWeekRange(82.3);

        await _handler.Handle(new UpsertDailyLogCommand(TestDate, 82.3, "Updated notes"), CancellationToken.None);

        saved.Should().BeSameAs(existing);
        saved!.WeightKg.Should().Be(82.3);
        saved.Notes.Should().Be("Updated notes");
        _dailyRepoMock.Verify(r => r.UpsertAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AfterUpsert_RecalculatesWeeklyAverage()
    {
        _dailyRepoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((DailyActualWeight?)null);
        _dailyRepoMock.Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);
        SetupWeekRange(80.0, 82.0, 78.0);

        WeeklyAverage? savedAvg = null;
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Callback<WeeklyAverage, CancellationToken>((e, _) => savedAvg = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new UpsertDailyLogCommand(TestDate, 78.0, null), CancellationToken.None);

        savedAvg.Should().NotBeNull();
        savedAvg!.WeekStart.Should().Be(Monday);
        savedAvg.AverageWeightKg.Should().BeApproximately(80.0, 0.001);
    }

    [Fact]
    public async Task Handle_WhenWeekHasNoEntries_DoesNotUpsertWeeklyAverage()
    {
        _dailyRepoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((DailyActualWeight?)null);
        _dailyRepoMock.Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);
        _dailyRepoMock.Setup(r => r.GetRangeAsync(Monday, Sunday, It.IsAny<CancellationToken>()))
                      .ReturnsAsync([]);

        await _handler.Handle(new UpsertDailyLogCommand(TestDate, 80.0, null), CancellationToken.None);

        _weeklyRepoMock.Verify(
            r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
