using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.ApplyRoutineForDate;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class ApplyRoutineForDateCommandHandlerTests
{
    private readonly Mock<IRoutineRepository>           _routineRepo        = new();
    private readonly Mock<IFoodLogRepository>           _foodRepo           = new();
    private readonly Mock<ICaloryLogRepository>         _caloryRepo         = new();
    private readonly Mock<ICustomActivityLogRepository> _customActivityRepo = new();
    private readonly ApplyRoutineForDateCommandHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 20);

    public ApplyRoutineForDateCommandHandlerTests()
    {
        _handler = new ApplyRoutineForDateCommandHandler(
            _routineRepo.Object,
            _foodRepo.Object,
            _caloryRepo.Object,
            _customActivityRepo.Object);

        _caloryRepo.Setup(r => r.AddRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((CaloryLog log, CancellationToken _) => log);
        _foodRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _customActivityRepo.Setup(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);
        _routineRepo.Setup(r => r.SetIsActiveForDateAsync(It.IsAny<DateOnly>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_EmptyRoutine_SetsActiveWithoutWritingLogs()
    {
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        _caloryRepo.Verify(r => r.AddRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _foodRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        _customActivityRepo.Verify(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodOnlyRoutine_CreatesFoodLogWithRoutineItemId_NoCaloryRepoCall()
    {
        var foodItem = new RoutineItem { SourceType = "food", Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([foodItem]);

        IEnumerable<FoodLog>? capturedFoodLogs = null;
        _foodRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
                 .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => capturedFoodLogs = logs)
                 .Returns(Task.CompletedTask);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        // Food items use the FoodLog navigation property — no separate calory log call
        _caloryRepo.Verify(r => r.AddRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Never);

        var foodLogs = capturedFoodLogs!.ToList();
        foodLogs.Should().ContainSingle();
        foodLogs[0].Date.Should().Be(TestDate);
        foodLogs[0].FoodItem.Should().Be("Oatmeal");
        foodLogs[0].Quantity.Should().Be("200g");
        foodLogs[0].CaloryLog.Should().NotBeNull();
        foodLogs[0].CaloryLog.Calories.Should().Be(150);
        foodLogs[0].CaloryLog.SourceType.Should().Be("food");
        foodLogs[0].CaloryLog.RoutineItemId.Should().Be(foodItem.Id);

        _customActivityRepo.Verify(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ActivityOnlyRoutine_CreatesCaloryLogAndCustomActivityLogWithRoutineItemId()
    {
        var actItem = new RoutineItem { SourceType = "activity", Description = "Walk 30min", Quantity = null, Calories = 120 };
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([actItem]);

        CaloryLog? capturedCaloryLog = null;
        _caloryRepo.Setup(r => r.AddRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()))
                   .Callback<CaloryLog, CancellationToken>((log, _) => capturedCaloryLog = log)
                   .ReturnsAsync((CaloryLog log, CancellationToken _) => log);

        CustomActivityLog? capturedCustomLog = null;
        _customActivityRepo.Setup(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()))
                           .Callback<CustomActivityLog, CancellationToken>((log, _) => capturedCustomLog = log)
                           .Returns(Task.CompletedTask);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        capturedCaloryLog.Should().NotBeNull();
        capturedCaloryLog!.Date.Should().Be(TestDate);
        capturedCaloryLog.Calories.Should().Be(120);
        capturedCaloryLog.SourceType.Should().Be("activity");
        capturedCaloryLog.Description.Should().Be("Walk 30min");
        capturedCaloryLog.RoutineItemId.Should().Be(actItem.Id);

        capturedCustomLog.Should().NotBeNull();
        capturedCustomLog!.Date.Should().Be(TestDate);
        capturedCustomLog.Description.Should().Be("Walk 30min");
        capturedCustomLog.CaloryLogId.Should().Be(capturedCaloryLog.Id);

        _foodRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MixedRoutine_CreatesBothFoodAndActivityEntries()
    {
        var foodItem = new RoutineItem { SourceType = "food", Description = "Toast", Quantity = "2 slices", Calories = 180 };
        var actItem  = new RoutineItem { SourceType = "activity", Description = "Run 5km", Quantity = null, Calories = 300 };
        _routineRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([foodItem, actItem]);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        _foodRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()), Times.Once);
        _caloryRepo.Verify(r => r.AddRoutineCopyAsync(It.IsAny<CaloryLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _customActivityRepo.Verify(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
