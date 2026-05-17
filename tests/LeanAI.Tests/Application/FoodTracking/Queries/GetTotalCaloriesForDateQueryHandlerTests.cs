using FluentAssertions;
using LeanAI.Application.FoodTracking.Queries.GetTotalCaloriesForDate;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Queries;

public class GetTotalCaloriesForDateQueryHandlerTests
{
    private readonly Mock<ICaloryLogRepository>          _repoMock = new();
    private readonly GetTotalCaloriesForDateQueryHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);

    public GetTotalCaloriesForDateQueryHandlerTests()
    {
        _handler = new GetTotalCaloriesForDateQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsTotal()
    {
        _repoMock
            .Setup(r => r.GetTotalCaloriesAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1847.5);

        var result = await _handler.Handle(new GetTotalCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().Be(1847.5);
    }

    [Fact]
    public async Task Handle_WhenNoEntries_ReturnsZero()
    {
        _repoMock
            .Setup(r => r.GetTotalCaloriesAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.0);

        var result = await _handler.Handle(new GetTotalCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().Be(0.0);
    }
}
