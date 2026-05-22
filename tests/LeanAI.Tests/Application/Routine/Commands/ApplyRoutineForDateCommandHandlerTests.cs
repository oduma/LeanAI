using FluentAssertions;
using LeanAI.Application.Routine.Commands.ApplyRoutineForDate;
using LeanAI.Domain.ActivityTracking.Entities;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Domain.Routine.Entities;
using LeanAI.Domain.Routine.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.Routine.Commands;

public class ApplyRoutineForDateCommandHandlerTests
{
    private readonly Mock<IRoutineRepository>           _routineRepo        = new();
    private readonly Mock<IFoodLogRepository>           _foodRepo           = new();
    private readonly Mock<IEnergyLogRepository>         _energyRepo         = new();
    private readonly Mock<ICustomActivityLogRepository> _customActivityRepo = new();
    private readonly ApplyRoutineForDateCommandHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 20);

    public ApplyRoutineForDateCommandHandlerTests()
    {
        _handler = new ApplyRoutineForDateCommandHandler(
            _routineRepo.Object,
            _foodRepo.Object,
            _energyRepo.Object,
            _customActivityRepo.Object);

        _energyRepo.Setup(r => r.AddRoutineCopyAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((EnergyLog log, CancellationToken _) => log);
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
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.AddRoutineCopyAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _foodRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        _customActivityRepo.Verify(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FoodOnlyRoutine_CreatesEnergyLogThenFoodLog()
    {
        var foodItem = new RoutineFoodItem { Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([foodItem]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        IEnumerable<FoodLog>? capturedFoodLogs = null;
        _foodRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()))
                 .Callback<IEnumerable<FoodLog>, CancellationToken>((logs, _) => capturedFoodLogs = logs)
                 .Returns(Task.CompletedTask);

        EnergyLog? capturedEnergyLog = null;
        _energyRepo.Setup(r => r.AddRoutineCopyAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()))
                   .Callback<EnergyLog, CancellationToken>((log, _) => capturedEnergyLog = log)
                   .ReturnsAsync((EnergyLog log, CancellationToken _) => log);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        capturedEnergyLog.Should().NotBeNull();
        capturedEnergyLog!.Calories.Should().Be(150);
        capturedEnergyLog.SourceType.Should().Be("food");
        capturedEnergyLog.RoutineItemId.Should().Be(foodItem.Id);

        var foodLogs = capturedFoodLogs!.ToList();
        foodLogs.Should().ContainSingle();
        foodLogs[0].Date.Should().Be(TestDate);
        foodLogs[0].FoodItem.Should().Be("Oatmeal");
        foodLogs[0].Quantity.Should().Be("200g");
        foodLogs[0].EnergyLogId.Should().Be(capturedEnergyLog.Id);

        _customActivityRepo.Verify(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ActivityOnlyRoutine_CreatesEnergyLogAndCustomActivityLog()
    {
        var actItem = new RoutineActivityItem { Description = "Walk 30min", Calories = 120 };
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([actItem]);

        EnergyLog? capturedEnergyLog = null;
        _energyRepo.Setup(r => r.AddRoutineCopyAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()))
                   .Callback<EnergyLog, CancellationToken>((log, _) => capturedEnergyLog = log)
                   .ReturnsAsync((EnergyLog log, CancellationToken _) => log);

        CustomActivityLog? capturedCustomLog = null;
        _customActivityRepo.Setup(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()))
                           .Callback<CustomActivityLog, CancellationToken>((log, _) => capturedCustomLog = log)
                           .Returns(Task.CompletedTask);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        capturedEnergyLog.Should().NotBeNull();
        capturedEnergyLog!.Calories.Should().Be(120);
        capturedEnergyLog.SourceType.Should().Be("activity");
        capturedEnergyLog.Description.Should().Be("Walk 30min");
        capturedEnergyLog.RoutineItemId.Should().Be(actItem.Id);

        capturedCustomLog.Should().NotBeNull();
        capturedCustomLog!.Date.Should().Be(TestDate);
        capturedCustomLog.Description.Should().Be("Walk 30min");
        capturedCustomLog.EnergyLogId.Should().Be(capturedEnergyLog.Id);

        _foodRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()), Times.Never);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MixedRoutine_CreatesBothFoodAndActivityEntries()
    {
        var foodItem = new RoutineFoodItem     { Description = "Toast",   Quantity = "2 slices", Calories = 180 };
        var actItem  = new RoutineActivityItem { Description = "Run 5km",                        Calories = 300 };
        _routineRepo.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([foodItem]);
        _routineRepo.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([actItem]);

        await _handler.Handle(new ApplyRoutineForDateCommand(TestDate), CancellationToken.None);

        _energyRepo.Verify(r => r.AddRoutineCopyAsync(It.IsAny<EnergyLog>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _foodRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<FoodLog>>(), It.IsAny<CancellationToken>()), Times.Once);
        _customActivityRepo.Verify(r => r.AddAsync(It.IsAny<CustomActivityLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _routineRepo.Verify(r => r.SetIsActiveForDateAsync(TestDate, true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
