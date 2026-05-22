using FluentAssertions;
using LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Queries;

public class GetFoodLogForDateQueryHandlerTests
{
    private readonly Mock<IFoodLogRepository>     _foodRepoMock   = new();
    private readonly Mock<IEnergyLogRepository>   _energyRepoMock = new();
    private readonly GetFoodLogForDateQueryHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);

    public GetFoodLogForDateQueryHandlerTests()
    {
        _handler = new GetFoodLogForDateQueryHandler(_foodRepoMock.Object, _energyRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsMappedDtos()
    {
        var energy1 = new EnergyLog { Date = TestDate, Calories = 248.5, SourceType = "food" };
        var energy2 = new EnergyLog { Date = TestDate, Calories = 220.0, SourceType = "food" };
        var food1   = new FoodLog   { Date = TestDate, FoodItem = "Grilled Chicken", Quantity = "150g", EnergyLogId = energy1.Id };
        var food2   = new FoodLog   { Date = TestDate, FoodItem = "Brown Rice",      Quantity = "200g", EnergyLogId = energy2.Id };

        _foodRepoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog> { food1, food2 });
        _energyRepoMock
            .Setup(r => r.GetManyByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EnergyLog> { energy1, energy2 });

        var result = await _handler.Handle(new GetFoodLogForDateQuery(TestDate), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(d =>
            d.FoodLogId == food1.Id && d.EnergyLogId == energy1.Id &&
            d.FoodItem == "Grilled Chicken" && d.Quantity == "150g" && d.Calories == 248.5);
        result.Should().ContainSingle(d =>
            d.FoodLogId == food2.Id && d.EnergyLogId == energy2.Id &&
            d.FoodItem == "Brown Rice" && d.Quantity == "200g" && d.Calories == 220.0);
    }

    [Fact]
    public async Task Handle_WhenNoEntries_ReturnsEmptyList()
    {
        _foodRepoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog>());

        var result = await _handler.Handle(new GetFoodLogForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
