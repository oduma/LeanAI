using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.UpsertDailyLog;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class UpsertDailyLogCommandHandlerTests
{
    private readonly Mock<IDailyActualWeightRepository> _repoMock = new();
    private readonly UpsertDailyLogCommandHandler      _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 13);

    public UpsertDailyLogCommandHandlerTests()
    {
        _handler = new UpsertDailyLogCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoExistingEntry_CreatesNewEntityAndUpserts()
    {
        DailyActualWeight? saved = null;
        _repoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((DailyActualWeight?)null);
        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
                 .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
                 .Returns(Task.CompletedTask);

        await _handler.Handle(new UpsertDailyLogCommand(TestDate, 82.3, "Feeling good"), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Date.Should().Be(TestDate);
        saved.WeightKg.Should().Be(82.3);
        saved.Notes.Should().Be("Feeling good");
        _repoMock.Verify(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryExists_UpdatesExistingEntityAndUpserts()
    {
        var existing = new DailyActualWeight { Date = TestDate, WeightKg = 80.0, Notes = "Old notes" };
        DailyActualWeight? saved = null;
        _repoMock.Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<DailyActualWeight>(), It.IsAny<CancellationToken>()))
                 .Callback<DailyActualWeight, CancellationToken>((e, _) => saved = e)
                 .Returns(Task.CompletedTask);

        await _handler.Handle(new UpsertDailyLogCommand(TestDate, 82.3, "Updated notes"), CancellationToken.None);

        saved.Should().BeSameAs(existing);
        saved!.WeightKg.Should().Be(82.3);
        saved.Notes.Should().Be("Updated notes");
        _repoMock.Verify(r => r.UpsertAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }
}
