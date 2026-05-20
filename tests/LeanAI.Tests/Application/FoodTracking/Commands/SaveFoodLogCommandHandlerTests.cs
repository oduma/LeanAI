using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.SaveFoodLog;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class SaveFoodLogCommandHandlerTests
{
    private readonly Mock<IFoodLogRepository> _repoMock = new();
    private readonly SaveFoodLogCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);
    private static readonly IReadOnlyList<FoodItemDto> TwoItems =
    [
        new FoodItemDto("Grilled Chicken", "150g", 248.5),
        new FoodItemDto("Brown Rice",      "200g", 220.0)
    ];

    public SaveFoodLogCommandHandlerTests()
    {
        _handler = new SaveFoodLogCommandHandler(_repoMock.Object);

        _repoMock
            .Setup(r => r.DeleteByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_EditMode_CallsDeleteThenAddWithCorrectEntities()
    {
        var deleteOrder  = 0;
        var addOrder     = 0;
        var callSequence = 0;

        _repoMock
            .Setup(r => r.DeleteByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .Callback(() => deleteOrder = ++callSequence)
            .Returns(Task.CompletedTask);

        IEnumerable<FoodLog>? captured = null;
        _repoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => { captured = logs; addOrder = ++callSequence; })
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SaveFoodLogCommand(TestDate, TwoItems, IsImportMode: false), CancellationToken.None);

        deleteOrder.Should().BeLessThan(addOrder, "DeleteByDate must be called before AddRange in edit mode");

        var logs = captured!.ToList();
        logs.Should().HaveCount(2);
        logs.Should().AllSatisfy(l => l.Date.Should().Be(TestDate));
        logs.Should().ContainSingle(l =>
            l.FoodItem == "Grilled Chicken" && l.Quantity == "150g" &&
            l.CaloryLog.Calories == 248.5 && l.CaloryLog.SourceType == "food");
        logs.Should().ContainSingle(l =>
            l.FoodItem == "Brown Rice" && l.Quantity == "200g" &&
            l.CaloryLog.Calories == 220.0 && l.CaloryLog.SourceType == "food");
    }

    [Fact]
    public async Task Handle_ImportMode_AddsWithoutDeleting()
    {
        IEnumerable<FoodLog>? captured = null;
        _repoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => captured = logs)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SaveFoodLogCommand(TestDate, TwoItems, IsImportMode: true), CancellationToken.None);

        _repoMock.Verify(r => r.DeleteByDateAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        captured!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_EditMode_WithEmptyItemsList_DeletesAndAddsEmpty()
    {
        await _handler.Handle(new SaveFoodLogCommand(TestDate, [], IsImportMode: false), CancellationToken.None);

        _repoMock.Verify(r => r.DeleteByDateAsync(TestDate, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<FoodLog>>(l => !l.Any()), It.IsAny<CancellationToken>()), Times.Once);
    }
}
