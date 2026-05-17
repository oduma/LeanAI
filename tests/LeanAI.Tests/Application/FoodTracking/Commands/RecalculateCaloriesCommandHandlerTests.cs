using FluentAssertions;
using LeanAI.Application.FoodTracking.Commands.RecalculateCalories;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Services;
using Moq;

namespace LeanAI.Tests.Application.FoodTracking.Commands;

public class RecalculateCaloriesCommandHandlerTests
{
    private readonly Mock<IFoodImageAnalysisService>   _serviceMock = new();
    private readonly RecalculateCaloriesCommandHandler _handler;

    private static readonly IReadOnlyList<FoodItemInputDto> Inputs =
    [
        new FoodItemInputDto("Pasta", "250g"),
        new FoodItemInputDto("Tomato Sauce", "100g")
    ];

    private static readonly IReadOnlyList<FoodItemDto> Updated =
    [
        new FoodItemDto("Pasta",        "250g",  310.0),
        new FoodItemDto("Tomato Sauce", "100g",   45.0)
    ];

    public RecalculateCaloriesCommandHandlerTests()
    {
        _handler = new RecalculateCaloriesCommandHandler(_serviceMock.Object);
    }

    [Fact]
    public async Task Handle_CallsServiceAndReturnsUpdatedCalories()
    {
        _serviceMock
            .Setup(s => s.RecalculateCaloriesAsync(Inputs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Updated);

        var result = await _handler.Handle(new RecalculateCaloriesCommand(Inputs), CancellationToken.None);

        result.Should().BeEquivalentTo(Updated);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        _serviceMock
            .Setup(s => s.RecalculateCaloriesAsync(It.IsAny<IReadOnlyList<FoodItemInputDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Could not extract food items from the provided image."));

        var act = async () =>
            await _handler.Handle(new RecalculateCaloriesCommand(Inputs), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
