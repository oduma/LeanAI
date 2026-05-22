using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.SaveFoodLog;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class SaveFoodLogCommandHandlerTests
{
    private readonly Mock<IFoodLogRepository>   _foodRepoMock   = new();
    private readonly Mock<IEnergyLogRepository> _energyRepoMock = new();
    private readonly SaveFoodLogCommandHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);
    private static readonly IReadOnlyList<FoodItemDto> TwoItems =
    [
        new FoodItemDto("Grilled Chicken", "150g", 248.5),
        new FoodItemDto("Brown Rice",      "200g", 220.0)
    ];

    public SaveFoodLogCommandHandlerTests()
    {
        _handler = new SaveFoodLogCommandHandler(_foodRepoMock.Object, _energyRepoMock.Object);

        _foodRepoMock
            .Setup(r => r.GetByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog>());
        _foodRepoMock
            .Setup(r => r.DeleteByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _foodRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _energyRepoMock
            .Setup(r => r.DeleteManyAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _energyRepoMock
            .Setup(r => r.AddAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnergyLog log, CancellationToken _) => log);
    }

    [Fact]
    public async Task Handle_EditMode_DeletesExistingWithExplicitCascadeThenAdds()
    {
        var existing = new List<FoodLog> { new() { EnergyLogId = Guid.NewGuid() } };
        _foodRepoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        IEnumerable<FoodLog>? captured = null;
        _foodRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => captured = logs)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SaveFoodLogCommand(TestDate, TwoItems, IsImportMode: false), CancellationToken.None);

        _foodRepoMock.Verify(r => r.DeleteByDateAsync(TestDate, It.IsAny<CancellationToken>()), Times.Once);
        _energyRepoMock.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 1), It.IsAny<CancellationToken>()), Times.Once);

        var logs = captured!.ToList();
        logs.Should().HaveCount(2);
        logs.Should().AllSatisfy(l => l.Date.Should().Be(TestDate));
        logs.Should().ContainSingle(l => l.FoodItem == "Grilled Chicken" && l.Quantity == "150g");
        logs.Should().ContainSingle(l => l.FoodItem == "Brown Rice" && l.Quantity == "200g");
    }

    [Fact]
    public async Task Handle_ImportMode_AddsWithoutDeleting()
    {
        IEnumerable<FoodLog>? captured = null;
        _foodRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => captured = logs)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SaveFoodLogCommand(TestDate, TwoItems, IsImportMode: true), CancellationToken.None);

        _foodRepoMock.Verify(r => r.DeleteByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        _energyRepoMock.Verify(r => r.DeleteManyAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        captured!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_EditMode_WithEmptyItemsList_DeletesAndAddsEmpty()
    {
        await _handler.Handle(new SaveFoodLogCommand(TestDate, [], IsImportMode: false), CancellationToken.None);

        _foodRepoMock.Verify(r => r.DeleteByDateAsync(TestDate, It.IsAny<CancellationToken>()), Times.Once);
        _foodRepoMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<FoodLog>>(l => !l.Any()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EachFoodItem_CreatesEnergyLogThenFoodLogWithEnergyLogId()
    {
        EnergyLog? capturedEnergyLog = null;
        _energyRepoMock
            .Setup(r => r.AddAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()))
            .Callback<EnergyLog, CancellationToken>((log, _) => capturedEnergyLog = log)
            .ReturnsAsync((EnergyLog log, CancellationToken _) => log);

        IEnumerable<FoodLog>? captured = null;
        _foodRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => captured = logs)
            .Returns(Task.CompletedTask);

        await _handler.Handle(
            new SaveFoodLogCommand(TestDate, [new FoodItemDto("Chicken", "100g", 180)], IsImportMode: true),
            CancellationToken.None);

        var log = captured!.Single();
        log.EnergyLogId.Should().Be(capturedEnergyLog!.Id);
    }
}
