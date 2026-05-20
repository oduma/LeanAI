using FluentAssertions;
using LeanAI.Application.ActivityTracking.Commands.EstimateActivityCalories;
using LeanAI.Application.ActivityTracking.Services;
using Moq;

namespace LeanAI.Tests.Application.ActivityTracking.Commands;

public class EstimateActivityCaloriesCommandHandlerTests
{
    private readonly Mock<IActivityCaloriesEstimationService>  _serviceMock = new();
    private readonly EstimateActivityCaloriesCommandHandler    _handler;

    public EstimateActivityCaloriesCommandHandlerTests()
    {
        _handler = new EstimateActivityCaloriesCommandHandler(_serviceMock.Object);
    }

    [Fact]
    public async Task Handle_DelegatesToEstimationService()
    {
        var descriptions = new List<string> { "5km run", "30min swim" };
        var expected     = new List<double> { 300.0, 250.0 };

        _serviceMock
            .Setup(s => s.EstimateAsync(descriptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(
            new EstimateActivityCaloriesCommand(descriptions), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
