using FluentAssertions;
using LeanAI.Application.FoodTracking.Queries.GetRoutineItems;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Queries;

public class GetRoutineItemsQueryHandlerTests
{
    private readonly Mock<IRoutineRepository> _repoMock = new();
    private readonly GetRoutineItemsQueryHandler _handler;

    public GetRoutineItemsQueryHandlerTests()
        => _handler = new GetRoutineItemsQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_EmptyRoutine_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        var result = await _handler.Handle(new GetRoutineItemsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RoutineHasItems_ReturnsMappedDtos()
    {
        var food = new RoutineItem { SourceType = "food", Description = "Oatmeal", Quantity = "200g", Calories = 150 };
        var act  = new RoutineItem { SourceType = "activity", Description = "Walk 30min", Quantity = null, Calories = 120 };

        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync([food, act]);

        var result = await _handler.Handle(new GetRoutineItemsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(d => d.SourceType == "food" && d.Description == "Oatmeal" && d.Quantity == "200g" && d.Calories == 150);
        result.Should().ContainSingle(d => d.SourceType == "activity" && d.Description == "Walk 30min" && d.Quantity == null && d.Calories == 120);
    }
}
