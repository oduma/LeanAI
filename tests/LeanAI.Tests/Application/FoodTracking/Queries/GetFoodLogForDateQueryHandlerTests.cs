using FluentAssertions;
using LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Queries;

public class GetFoodLogForDateQueryHandlerTests
{
    private readonly Mock<IFoodLogRepository>     _repoMock = new();
    private readonly GetFoodLogForDateQueryHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);

    public GetFoodLogForDateQueryHandlerTests()
    {
        _handler = new GetFoodLogForDateQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsMappedDtos()
    {
        var calory1 = new CaloryLog { Date = TestDate, Calories = 248.5, SourceType = "food" };
        var calory2 = new CaloryLog { Date = TestDate, Calories = 220.0, SourceType = "food" };
        var food1   = new FoodLog   { Date = TestDate, FoodItem = "Grilled Chicken", Quantity = "150g", CaloryLog = calory1 };
        var food2   = new FoodLog   { Date = TestDate, FoodItem = "Brown Rice",      Quantity = "200g", CaloryLog = calory2 };
        food1.CaloryLogId = calory1.Id; // CaloryLogId must match for the DTO assertion
        food2.CaloryLogId = calory2.Id;

        _repoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog> { food1, food2 });

        var result = await _handler.Handle(new GetFoodLogForDateQuery(TestDate), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(d =>
            d.FoodLogId == food1.Id && d.CaloryLogId == calory1.Id &&
            d.FoodItem == "Grilled Chicken" && d.Quantity == "150g" && d.Calories == 248.5);
        result.Should().ContainSingle(d =>
            d.FoodLogId == food2.Id && d.CaloryLogId == calory2.Id &&
            d.FoodItem == "Brown Rice" && d.Quantity == "200g" && d.Calories == 220.0);
    }

    [Fact]
    public async Task Handle_WhenNoEntries_ReturnsEmptyList()
    {
        _repoMock
            .Setup(r => r.GetByDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FoodLog>());

        var result = await _handler.Handle(new GetFoodLogForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
