using FluentAssertions;
using LeanAI.Application.Routine.Queries.GetRoutineItems;
using LeanAI.Domain.Routine.Entities;
using LeanAI.Domain.Routine.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.Routine.Queries;

public class GetRoutineItemsQueryHandlerTests
{
    private readonly Mock<IRoutineRepository>     _repoMock = new();
    private readonly GetRoutineItemsQueryHandler  _handler;

    public GetRoutineItemsQueryHandlerTests()
        => _handler = new GetRoutineItemsQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_EmptyRoutine_ReturnsBothEmptyLists()
    {
        _repoMock.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repoMock.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _handler.Handle(new GetRoutineItemsQuery(), CancellationToken.None);

        result.FoodItems.Should().BeEmpty();
        result.ActivityItems.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FoodAndActivityItems_ReturnsMappedDtos()
    {
        var food = new RoutineFoodItem     { Description = "Oatmeal",  Quantity = "200g", Calories = 150 };
        var act  = new RoutineActivityItem { Description = "Walk 30min",               Calories = 120 };

        _repoMock.Setup(r => r.GetAllFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([food]);
        _repoMock.Setup(r => r.GetAllActivityItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([act]);

        var result = await _handler.Handle(new GetRoutineItemsQuery(), CancellationToken.None);

        result.FoodItems.Should().ContainSingle(d =>
            d.Description == "Oatmeal" && d.Quantity == "200g" && d.Calories == 150);
        result.ActivityItems.Should().ContainSingle(d =>
            d.Description == "Walk 30min" && d.Calories == 120);
    }
}
