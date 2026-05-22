using FluentAssertions;
using LeanAI.Application.Routine.Commands.RemoveUnmodifiedRoutineItems;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Domain.Routine.Entities;
using LeanAI.Domain.Routine.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.Routine.Commands;

public class RemoveUnmodifiedRoutineItemsForDateCommandHandlerTests
{
    private readonly Mock<IRoutineRepository>           _routineRepo        = new();
    private readonly Mock<IEnergyLogRepository>         _energyRepo         = new();
    private readonly Mock<IFoodLogRepository>           _foodRepo           = new();
    private readonly Mock<ICustomActivityLogRepository> _customActivityRepo = new();
    private readonly RemoveUnmodifiedRoutineItemsForDateCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 20);

    public RemoveUnmodifiedRoutineItemsForDateCommandHandlerTests()
    {
        _handler = new RemoveUnmodifiedRoutineItemsForDateCommandHandler(
            _routineRepo.Object,
            _energyRepo.Object,
            _foodRepo.Object,
            _customActivityRepo.Object);

        _energyRepo.Setup(r => r.DeleteManyAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);
        _foodRepo.Setup(r => r.DeleteByEnergyLogIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _customActivityRepo.Setup(r => r.DeleteByEnergyLogIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);
        _routineRepo.Setup(r => r.SetIsActiveForDateAsync(It.IsAny<DateOnly>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_NoRoutineCopies_SetsInactiveWithoutDeleting()
    {
        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodItemUnmodified_DeletesViaExplicitCascade()
    {
        var routineItem = new RoutineFoodItem { Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var energyLog   = new EnergyLog { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog   { FoodItem = "Oatmeal", Quantity = "200g", EnergyLogId = energyLog.Id };

        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([energyLog]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _foodRepo.Setup(r => r.GetByEnergyLogIdAsync(energyLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _foodRepo.Verify(r => r.DeleteByEnergyLogIdsAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Contains(energyLog.Id)), It.IsAny<CancellationToken>()), Times.Once);
        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Contains(energyLog.Id)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodItemCaloriesChanged_Preserves()
    {
        var routineItem = new RoutineFoodItem { Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var energyLog   = new EnergyLog { Date = TestDate, Calories = 999, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog   { FoodItem = "Oatmeal", Quantity = "200g", EnergyLogId = energyLog.Id };

        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([energyLog]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _foodRepo.Setup(r => r.GetByEnergyLogIdAsync(energyLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodItemNameChanged_Preserves()
    {
        var routineItem = new RoutineFoodItem { Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var energyLog   = new EnergyLog { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog   { FoodItem = "Porridge", Quantity = "200g", EnergyLogId = energyLog.Id };

        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([energyLog]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _foodRepo.Setup(r => r.GetByEnergyLogIdAsync(energyLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ActivityItemUnmodified_DeletesViaExplicitCascade()
    {
        var routineItem = new RoutineActivityItem { Description = "Walk 30min", Calories = 120 };
        var energyLog   = new EnergyLog { Date = TestDate, Calories = 120, SourceType = "activity", Description = "Walk 30min", RoutineItemId = routineItem.Id };

        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([energyLog]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _customActivityRepo.Verify(r => r.DeleteByEnergyLogIdsAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Contains(energyLog.Id)), It.IsAny<CancellationToken>()), Times.Once);
        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Contains(energyLog.Id)), It.IsAny<CancellationToken>()), Times.Once);
        _foodRepo.Verify(r => r.GetByEnergyLogIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActivityItemDescriptionChanged_Preserves()
    {
        var routineItem = new RoutineActivityItem { Description = "Walk 30min", Calories = 120 };
        var energyLog   = new EnergyLog { Date = TestDate, Calories = 120, SourceType = "activity", Description = "Jog 30min", RoutineItemId = routineItem.Id };

        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([energyLog]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RoutineItemDeleted_PreservesEnergyLog()
    {
        var energyLog = new EnergyLog { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = Guid.NewGuid() };

        _energyRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([energyLog]);
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.DeleteManyAsync(
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }
}
