using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.DeleteFoodLog;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class DeleteFoodLogForDateCommandHandlerTests
{
    private readonly Mock<IFoodLogRepository>            _foodRepoMock   = new();
    private readonly Mock<IEnergyLogRepository>          _energyRepoMock = new();
    private readonly DeleteFoodLogForDateCommandHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);

    public DeleteFoodLogForDateCommandHandlerTests()
    {
        _handler = new DeleteFoodLogForDateCommandHandler(_foodRepoMock.Object, _energyRepoMock.Object);

        _foodRepoMock
            .Setup(r => r.GetByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog>());
        _foodRepoMock
            .Setup(r => r.DeleteByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _energyRepoMock
            .Setup(r => r.DeleteManyAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_CallsDeleteByDateWithCorrectDate()
    {
        await _handler.Handle(new DeleteFoodLogForDateCommand(TestDate), CancellationToken.None);

        _foodRepoMock.Verify(
            r => r.DeleteByDateAsync(TestDate, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeletesLinkedEnergyLogsExplicitly()
    {
        var energyLogId = Guid.NewGuid();
        _foodRepoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog> { new() { EnergyLogId = energyLogId } });

        await _handler.Handle(new DeleteFoodLogForDateCommand(TestDate), CancellationToken.None);

        _energyRepoMock.Verify(
            r => r.DeleteManyAsync(
                It.Is<IReadOnlyList<Guid>>(l => l.Contains(energyLogId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
