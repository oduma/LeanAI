using FluentAssertions;
using LeanAI.Application.EnergyTracking.Queries.GetActivityCaloriesForDate;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.EnergyTracking.Queries;

public class GetActivityCaloriesForDateQueryHandlerTests
{
    private readonly Mock<IEnergyLogRepository>              _repoMock = new();
    private readonly GetActivityCaloriesForDateQueryHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 18);

    public GetActivityCaloriesForDateQueryHandlerTests()
    {
        _handler = new GetActivityCaloriesForDateQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_MapsEnergyLogsToActivityEnergyLogDtos()
    {
        var logs = new List<EnergyLog>
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
            .ReturnsAsync(new List<EnergyLog>());

        var result = await _handler.Handle(
            new GetActivityCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
