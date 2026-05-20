using FluentAssertions;
using LeanAI.Application.FoodTracking.Queries.GetRoutineStatusForDate;
using LeanAI.Domain.FoodTracking.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Queries;

public class GetRoutineStatusForDateQueryHandlerTests
{
    private readonly Mock<IRoutineRepository> _repoMock = new();
    private readonly GetRoutineStatusForDateQueryHandler _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 20);

    public GetRoutineStatusForDateQueryHandlerTests()
        => _handler = new GetRoutineStatusForDateQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_NoStatusForDate_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetIsActiveForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var result = await _handler.Handle(new GetRoutineStatusForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_StatusIsActive_ReturnsTrue()
    {
        _repoMock.Setup(r => r.GetIsActiveForDateAsync(TestDate, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var result = await _handler.Handle(new GetRoutineStatusForDateQuery(TestDate), CancellationToken.None);

        result.Should().BeTrue();
    }
}
