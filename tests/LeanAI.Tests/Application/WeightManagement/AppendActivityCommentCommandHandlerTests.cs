using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.AppendActivityComment;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class AppendActivityCommentCommandHandlerTests
{
    private readonly Mock<IDailyActualWeightRepository> _repoMock = new();
    private readonly AppendActivityCommentCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);
    private const string Comment = "I run for 5.2km at a pace of 5:30min/km. Total time: 28:36min.";

    public AppendActivityCommentCommandHandlerTests()
    {
        _handler = new AppendActivityCommentCommandHandler(_repoMock.Object);

        _repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_WhenEntryExistsWithNullNotes_SetsNotesToComment()
    {
        var existing = new DailyActualWeight { Date = TestDate, WeightKg = 80.0, Notes = null };
        _repoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        DailyActualWeight? saved = null;
        _repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
            .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AppendActivityCommentCommand(TestDate, Comment), CancellationToken.None);

        saved.Should().BeSameAs(existing);
        saved!.Notes.Should().Be(Comment);
    }

    [Fact]
    public async Task Handle_WhenEntryExistsWithEmptyNotes_SetsNotesToComment()
    {
        var existing = new DailyActualWeight { Date = TestDate, WeightKg = 80.0, Notes = "" };
        _repoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        DailyActualWeight? saved = null;
        _repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
            .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AppendActivityCommentCommand(TestDate, Comment), CancellationToken.None);

        saved!.Notes.Should().Be(Comment);
    }

    [Fact]
    public async Task Handle_WhenEntryExistsWithExistingNotes_AppendsCommentOnNewLine()
    {
        var existing = new DailyActualWeight { Date = TestDate, WeightKg = 80.0, Notes = "existing" };
        _repoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        DailyActualWeight? saved = null;
        _repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
            .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AppendActivityCommentCommand(TestDate, Comment), CancellationToken.None);

        saved!.Notes.Should().Be($"existing\n{Comment}");
    }

    [Fact]
    public async Task Handle_WhenNoEntry_CreatesNewEntryWithWeightZeroAndComment()
    {
        _repoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyActualWeight?)null);

        DailyActualWeight? saved = null;
        _repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
            .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new AppendActivityCommentCommand(TestDate, Comment), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Date.Should().Be(TestDate);
        saved.WeightKg.Should().Be(0);
        saved.Notes.Should().Be(Comment);
        _repoMock.Verify(
            r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
