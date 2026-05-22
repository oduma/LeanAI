using FluentAssertions;
using LeanAI.Application.EnergyTracking.Queries.GetTotalCaloriesForDate;
using LeanAI.Domain.EnergyTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.EnergyTracking.Queries;

public class GetTotalCaloriesForDateQueryHandlerTests
{
    private readonly Mock<IEnergyLogRepository>          _repoMock = new();
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
    public async Task Handle_WhenNetIsZero_ReturnsZero()
    {
        _repoMock
            .Setup(r => r.GetTotalCaloriesAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)0.0);

        var result = await _handler.Handle(new GetTotalCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_WhenNoData_ReturnsNull()
    {
        _repoMock
            .Setup(r => r.GetTotalCaloriesAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        var result = await _handler.Handle(new GetTotalCaloriesForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeNull();
    }
}
