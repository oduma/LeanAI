using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.AnalyzeFoodImage;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Services;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class AnalyzeFoodImageCommandHandlerTests
{
    private readonly Mock<IFoodImageAnalysisService> _serviceMock = new();
    private readonly AnalyzeFoodImageCommandHandler  _handler;

    private static readonly DateOnly TestDate = new(2026, 5, 16);
    private static readonly IReadOnlyList<FoodItemDto> ThreeItems =
    [
        new FoodItemDto("Grilled Chicken", "150g",  248.5),
        new FoodItemDto("Brown Rice",      "200g",  220.0),
        new FoodItemDto("Salad",           "100g",   35.0)
    ];

    public AnalyzeFoodImageCommandHandlerTests()
    {
        _handler = new AnalyzeFoodImageCommandHandler(_serviceMock.Object);
    }

    [Fact]
    public async Task Handle_CallsServiceAndReturnsResult()
    {
        _serviceMock
            .Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThreeItems);

        var result = await _handler.Handle(
            new AnalyzeFoodImageCommand([1, 2, 3], "image/jpeg", TestDate), CancellationToken.None);

        result.Should().BeEquivalentTo(ThreeItems);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        _serviceMock
            .Setup(s => s.AnalyzeImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Could not extract food items from the provided image."));

        var act = async () =>
            await _handler.Handle(new AnalyzeFoodImageCommand([1], "image/jpeg", TestDate), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not extract food items from the provided image.");
    }
}
