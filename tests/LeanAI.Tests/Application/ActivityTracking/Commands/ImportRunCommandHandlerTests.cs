using FluentAssertions;
using LeanAI.Application.ActivityTracking.Commands.ImportRun;
using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.ActivityTracking.Services;
using LeanAI.Application.WeightManagement.Commands.AppendActivityComment;
using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using MediatR;
using Moq;

namespace LeanAI.Tests.Application.ActivityTracking.Commands;

public class ImportRunCommandHandlerTests
{
    private readonly Mock<IRunImageAnalysisService> _serviceMock  = new();
    private readonly Mock<IActivityLogRepository>   _repoMock     = new();
    private readonly Mock<IMediator>                _mediatorMock = new();
    private readonly ImportRunCommandHandler        _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);

    private static readonly IReadOnlyList<ActivityMetricDto> ThreeMetrics =
    [
        new ActivityMetricDto("distance", "5.2", "km"),
        new ActivityMetricDto("pace",     "5:30", "min/km"),
        new ActivityMetricDto("duration", "28:36", "min")
    ];

    public ImportRunCommandHandlerTests()
    {
        _handler = new ImportRunCommandHandler(
            _serviceMock.Object,
            _repoMock.Object,
            _mediatorMock.Object);

        _repoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<ActivityLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // AppendActivityCommentCommand : IRequest (void) → Send returns Task
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AppendActivityCommentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_SuccessPath_CallsAddRangeWithThreeRows()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThreeMetrics);

        IEnumerable<ActivityLog>? captured = null;
        _repoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<ActivityLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ActivityLog>, CancellationToken>((logs, _) => captured = logs)
            .Returns(Task.CompletedTask);

        var cmd = new ImportRunCommand([1, 2, 3], "image/jpeg", TestDate);
        await _handler.Handle(cmd, CancellationToken.None);

        var logs = captured!.ToList();
        logs.Should().HaveCount(3);
        logs.Should().AllSatisfy(l =>
        {
            l.Date.Should().Be(TestDate);
            l.Activity.Should().Be("run");
        });
        logs.Should().ContainSingle(l => l.ParameterName == "distance" && l.Value == "5.2"   && l.Unit == "km");
        logs.Should().ContainSingle(l => l.ParameterName == "pace"     && l.Value == "5:30"  && l.Unit == "min/km");
        logs.Should().ContainSingle(l => l.ParameterName == "duration" && l.Value == "28:36" && l.Unit == "min");
    }

    [Fact]
    public async Task Handle_SuccessPath_DispatchesAppendCommentWithCorrectText()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThreeMetrics);

        AppendActivityCommentCommand? capturedCmd = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AppendActivityCommentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((r, _) => capturedCmd = r as AppendActivityCommentCommand)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new ImportRunCommand([1], "image/jpeg", TestDate), CancellationToken.None);

        capturedCmd.Should().NotBeNull();
        capturedCmd!.Date.Should().Be(TestDate);
        capturedCmd.Comment.Should().Be(
            "I run for 5.2km at a pace of 5:30min/km. Total time: 28:36min.");
    }

    [Fact]
    public async Task Handle_SuccessPath_ReturnsMetricsList()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThreeMetrics);

        var result = await _handler.Handle(
            new ImportRunCommand([1], "image/jpeg", TestDate), CancellationToken.None);

        result.Should().BeEquivalentTo(ThreeMetrics);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesExceptionWithoutCallingRepoOrMediator()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Could not extract run metrics from the provided image."));

        var act = async () =>
            await _handler.Handle(new ImportRunCommand([1], "image/jpeg", TestDate), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not extract run metrics from the provided image.");

        _repoMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<ActivityLog>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
