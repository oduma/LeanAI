using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.RecalculateWeeklyAverage;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class RecalculateWeeklyAverageCommandHandlerTests
{
    private readonly Mock<IDailyActualWeightRepository> _dailyRepoMock = new();
    private readonly Mock<IWeeklyAverageRepository>     _weeklyRepoMock = new();
    private readonly RecalculateWeeklyAverageCommandHandler _handler;

    public RecalculateWeeklyAverageCommandHandlerTests()
    {
        _handler = new RecalculateWeeklyAverageCommandHandler(
            _dailyRepoMock.Object, _weeklyRepoMock.Object);
    }

    // 2026-05-11 is a Monday
    private static readonly DateOnly Monday    = new(2026, 5, 11);
    private static readonly DateOnly Wednesday = new(2026, 5, 13);
    private static readonly DateOnly Sunday    = new(2026, 5, 17);

    private void SetupDailyEntries(DateOnly from, DateOnly to, params double[] weights)
    {
        var entries = weights.Select((w, i) =>
            new DailyActualWeight { Date = from.AddDays(i), WeightKg = w }).ToList();
        _dailyRepoMock
            .Setup(r => r.GetRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
    }

    [Fact]
    public async Task Handle_SingleEntry_UpsertsAverageThatEntry()
    {
        SetupDailyEntries(Monday, Sunday, 80.0);
        WeeklyAverage? saved = null;
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Callback<WeeklyAverage, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new RecalculateWeeklyAverageCommand(Monday), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.WeekStart.Should().Be(Monday);
        saved.AverageWeightKg.Should().Be(80.0);
    }

    [Fact]
    public async Task Handle_MultipleEntries_UpsertsCorrectMean()
    {
        SetupDailyEntries(Monday, Sunday, 80.0, 82.0, 78.0);
        WeeklyAverage? saved = null;
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Callback<WeeklyAverage, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new RecalculateWeeklyAverageCommand(Monday), CancellationToken.None);

        saved!.AverageWeightKg.Should().BeApproximately(80.0, 0.001);
    }

    [Fact]
    public async Task Handle_DateOnWednesday_NormalisesToMonday()
    {
        SetupDailyEntries(Monday, Sunday, 80.0);
        WeeklyAverage? saved = null;
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Callback<WeeklyAverage, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new RecalculateWeeklyAverageCommand(Wednesday), CancellationToken.None);

        _dailyRepoMock.Verify(
            r => r.GetRangeAsync(Monday, Sunday, It.IsAny<CancellationToken>()), Times.Once);
        saved!.WeekStart.Should().Be(Monday);
    }

    [Fact]
    public async Task Handle_DateOnSunday_NormalisesToMonday()
    {
        SetupDailyEntries(Monday, Sunday, 80.0);
        _weeklyRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new RecalculateWeeklyAverageCommand(Sunday), CancellationToken.None);

        _dailyRepoMock.Verify(
            r => r.GetRangeAsync(Monday, Sunday, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoEntriesInWeek_DoesNotUpsert()
    {
        _dailyRepoMock
            .Setup(r => r.GetRangeAsync(Monday, Sunday, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _handler.Handle(new RecalculateWeeklyAverageCommand(Monday), CancellationToken.None);

        _weeklyRepoMock.Verify(
            r => r.UpsertAsync(It.IsAny<WeeklyAverage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
