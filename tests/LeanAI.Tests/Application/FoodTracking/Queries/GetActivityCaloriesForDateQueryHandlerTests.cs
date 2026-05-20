using FluentAssertions;
using LeanAI.Application.FoodTracking.Queries.GetActivityCaloriesForDate;
using LeanAI.Domain.FoodTracking.Entities;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Queries;

public class GetActivityCaloriesForDateQueryHandlerTests
{
    private readonly Mock<ICaloryLogRepository>              _repoMock = new();
    private readonly GetActivityCaloriesForDateQueryHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 18);

    public GetActivityCaloriesForDateQueryHandlerTests()
    {
        _handler = new GetActivityCaloriesForDateQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_MapsCaloryLogsToActivityCaloryLogDtos()
    {
        var logs = new List<CaloryLog>
        {
            new() { Date = TestDate, Calories = 320, Description = "Run",  SourceType = "activity" },
            new() { Date = TestDate, Calories = 200, Description = "Swim", SourceType = "activity" }
        };

        _repoMock
            .Setup(r => r.GetActivityCaloriesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var result = await _handler.Handle(
            new GetActivityCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(d => d.Description == "Run"  && d.Calories == 320);
        result.Should().ContainSingle(d => d.Description == "Swim" && d.Calories == 200);
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyList()
    {
        _repoMock
            .Setup(r => r.GetActivityCaloriesForDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CaloryLog>());

        var result = await _handler.Handle(
            new GetActivityCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
