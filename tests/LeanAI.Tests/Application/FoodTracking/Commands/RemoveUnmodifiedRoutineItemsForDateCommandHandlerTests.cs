using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.RemoveUnmodifiedRoutineItems;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class RemoveUnmodifiedRoutineItemsForDateCommandHandlerTests
{
    private readonly Mock<IRoutineRepository>   _routineRepo = new();
    private readonly Mock<ICaloryLogRepository> _caloryRepo  = new();
    private readonly Mock<IFoodLogRepository>   _foodRepo    = new();
    private readonly RemoveUnmodifiedRoutineItemsForDateCommandHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 20);

    public RemoveUnmodifiedRoutineItemsForDateCommandHandlerTests()
    {
        _handler = new RemoveUnmodifiedRoutineItemsForDateCommandHandler(
            _routineRepo.Object,
            _caloryRepo.Object,
            _foodRepo.Object);

        _caloryRepo.Setup(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);
        _routineRepo.Setup(r => r.SetIsActiveForDateAsync(It.IsAny<DateOnly>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_NoRoutineCopies_SetsInactiveWithoutDeleting()
    {
        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodItemUnmodified_DeletesCaloryLog()
    {
        var routineItem = new RoutineItem { SourceType = "food", Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog    { FoodItem = "Oatmeal", Quantity = "200g", CaloryLogId = caloryLog.Id, CaloryLog = caloryLog };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _foodRepo.Setup(r => r.GetByCaloryLogIdAsync(caloryLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(caloryLog, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodItemCaloriesChanged_Preserves()
    {
        var routineItem = new RoutineItem { SourceType = "food", Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 999, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog    { FoodItem = "Oatmeal", Quantity = "200g", CaloryLogId = caloryLog.Id, CaloryLog = caloryLog };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _foodRepo.Setup(r => r.GetByCaloryLogIdAsync(caloryLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FoodItemNameChanged_Preserves()
    {
        var routineItem = new RoutineItem { SourceType = "food", Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog    { FoodItem = "Porridge", Quantity = "200g", CaloryLogId = caloryLog.Id, CaloryLog = caloryLog };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _foodRepo.Setup(r => r.GetByCaloryLogIdAsync(caloryLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FoodItemQuantityChanged_Preserves()
    {
        var routineItem = new RoutineItem { SourceType = "food", Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = routineItem.Id };
        var foodLog     = new FoodLog    { FoodItem = "Oatmeal", Quantity = "300g", CaloryLogId = caloryLog.Id, CaloryLog = caloryLog };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);
        _foodRepo.Setup(r => r.GetByCaloryLogIdAsync(caloryLog.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(foodLog);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActivityItemUnmodified_DeletesCaloryLog()
    {
        var routineItem = new RoutineItem { SourceType = "activity", Description = "Walk 30min", Calories = 120 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 120, SourceType = "activity", Description = "Walk 30min", RoutineItemId = routineItem.Id };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(caloryLog, It.IsAny<CancellationToken>()), Times.Once);
        // No food log lookup for activity items
        _foodRepo.Verify(r => r.GetByCaloryLogIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActivityItemDescriptionChanged_Preserves()
    {
        var routineItem = new RoutineItem { SourceType = "activity", Description = "Walk 30min", Calories = 120 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 120, SourceType = "activity", Description = "Jog 30min", RoutineItemId = routineItem.Id };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActivityItemCaloriesChanged_Preserves()
    {
        var routineItem = new RoutineItem { SourceType = "activity", Description = "Walk 30min", Calories = 120 };
        var caloryLog   = new CaloryLog  { Date = TestDate, Calories = 999, SourceType = "activity", Description = "Walk 30min", RoutineItemId = routineItem.Id };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([routineItem]);

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RoutineItemDeleted_PreservesCaloryLog()
    {
        // RoutineItem was deleted from routine after the copy was applied
        var caloryLog = new CaloryLog { Date = TestDate, Calories = 150, SourceType = "food", RoutineItemId = Guid.NewGuid() };

        _caloryRepo.Setup(r => r.GetRoutineCopiesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([caloryLog]);
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]); // empty routine

        await _handler.Handle(new RemoveUnmodifiedRoutineItemsForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.DeleteRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
