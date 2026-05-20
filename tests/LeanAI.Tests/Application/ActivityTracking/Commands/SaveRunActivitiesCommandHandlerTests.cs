using FluentAssertions;
using LeanAI.Application.ActivityTracking.Commands.SaveRunActivities;
using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.WeightManagement.Commands.AppendActivityComment;
using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;
using Moq;

namespace LeanAI.Tests.Application.ActivityTracking.Commands;

public class SaveRunActivitiesCommandHandlerTests
{
    private readonly Mock<ICaloryLogRepository>          _caloryRepoMock          = new();
    private readonly Mock<ICustomActivityLogRepository>  _customActivityRepoMock  = new();
    private readonly Mock<IActivityLogRepository>        _activityRepoMock        = new();
    private readonly Mock<IMediator>                     _mediatorMock            = new();
    private readonly SaveRunActivitiesCommandHandler     _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 18);

    private static readonly IReadOnlyList<ActivityMetricDto> Metrics =
    [
        new ActivityMetricDto("distance", "5.2",  "km"),
        new ActivityMetricDto("pace",     "5:30", "min/km"),
        new ActivityMetricDto("duration", "28:36","min")
    ];

    public SaveRunActivitiesCommandHandlerTests()
    {
        _handler = new SaveRunActivitiesCommandHandler(
            _caloryRepoMock.Object,
            _customActivityRepoMock.Object,
            _activityRepoMock.Object,
            _mediatorMock.Object);

        _caloryRepoMock
            .Setup(r => r.DeleteActivityCaloriesForDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _caloryRepoMock
            .Setup(r => r.AddActivityAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly d, double cal, string desc, CancellationToken _) => new CaloryLog
            {
                Date        = d,
                Calories    = cal,
                Description = desc,
                SourceType  = "activity"
            });

        _activityRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<ActivityLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _customActivityRepoMock
            .Setup(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AppendActivityCommentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_AlwaysDeletesActivityCaloriesFirst()
    {
        var cmd = new SaveRunActivitiesCommand(TestDate, [], IsImportMode: false);

        await _handler.Handle(cmd, CancellationToken.None);

        _caloryRepoMock.Verify(
            r => r.DeleteActivityCaloriesForDateAsync(TestDate, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ImportMode_RunRow_SavesMetricsAndAppendsComment()
    {
        var rows = new List<RunActivityRowDto>
        {
            new("I run for 5.2km...", 320.0, IsRunRow: true, Metrics)
        };
        var cmd = new SaveRunActivitiesCommand(TestDate, rows, IsImportMode: true);

        await _handler.Handle(cmd, CancellationToken.None);

        _activityRepoMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<ActivityLog>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AppendActivityCommentCommand>(c => c.Date == TestDate && c.Comment == "I run for 5.2km..."),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ImportMode_RunRow_AddsCaloryLog()
    {
        var rows = new List<RunActivityRowDto>
        {
            new("I run for 5.2km...", 320.0, IsRunRow: true, Metrics)
        };

        await _handler.Handle(new SaveRunActivitiesCommand(TestDate, rows, true), CancellationToken.None);

        _caloryRepoMock.Verify(
            r => r.AddActivityAsync(TestDate, 320.0, "I run for 5.2km...", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CustomRow_AddsCaloryLogAndCustomActivityLog()
    {
        var rows = new List<RunActivityRowDto>
        {
            new("30 minute swim", 200.0, IsRunRow: false)
        };

        await _handler.Handle(new SaveRunActivitiesCommand(TestDate, rows, false), CancellationToken.None);

        _caloryRepoMock.Verify(
            r => r.AddActivityAsync(TestDate, 200.0, "30 minute swim", It.IsAny<CancellationToken>()),
            Times.Once);

        _customActivityRepoMock.Verify(
            r => r.AddAsync(
                It.Is<CustomActivityLog>(l => l.Date == TestDate && l.Description == "30 minute swim"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EditMode_RunRow_DoesNotSaveMetrics_ButDoesAppendComment()
    {
        var rows = new List<RunActivityRowDto>
        {
            new("Some run", 300.0, IsRunRow: true, Metrics)
        };

        await _handler.Handle(new SaveRunActivitiesCommand(TestDate, rows, IsImportMode: false), CancellationToken.None);

        _activityRepoMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<ActivityLog>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mediatorMock.Verify(
            m => m.Send(
                It.Is<AppendActivityCommentCommand>(c => c.Date == TestDate && c.Comment == "Some run"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
