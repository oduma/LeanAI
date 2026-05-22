using FluentAssertions;
using LeanAI.Application.WeightManagement.Queries.GetBmrForDate;
using LeanAI.Domain.EnergyTracking.Entities;
using LeanAI.Domain.EnergyTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GetBmrForDateQueryHandlerTests
{
    private readonly Mock<IEnergyLogRepository> _repoMock = new();
    private readonly GetBmrForDateQueryHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 20);

    public GetBmrForDateQueryHandlerTests()
    {
        _handler = new GetBmrForDateQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_BmrExists_ReturnsCalories()
    {
        _repoMock
            .Setup(r => r.GetBmrForDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnergyLog { Date = TestDate, Calories = 1738.75, SourceType = "bmr" });

        var result = await _handler.Handle(new GetBmrForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeApproximately(1738.75, precision: 0.01);
    }

    [Fact]
    public async Task Handle_NoBmr_ReturnsNull()
    {
        _repoMock
            .Setup(r => r.GetBmrForDateAsync(TestDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnergyLog?)null);

        var result = await _handler.Handle(new GetBmrForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeNull();
    }
}
