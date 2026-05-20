using FluentAssertions;
using LeanAI.Application.ActivityTracking.Commands.AnalyzeRunImage;
using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.ActivityTracking.Services;
using Moq;

namespace LeanAI.Tests.Application.ActivityTracking.Commands;

public class AnalyzeRunImageCommandHandlerTests
{
    private readonly Mock<IRunImageAnalysisService> _serviceMock = new();
    private readonly AnalyzeRunImageCommandHandler  _handler;

    private static readonly IReadOnlyList<ActivityMetricDto> ThreeMetrics =
    [
        new ActivityMetricDto("distance", "5.2",  "km"),
        new ActivityMetricDto("pace",     "5:30", "min/km"),
        new ActivityMetricDto("duration", "28:36","min")
    ];

    private static readonly IReadOnlyList<ActivityMetricDto> FourMetrics =
    [
        new ActivityMetricDto("distance",        "5.2",  "km"),
        new ActivityMetricDto("pace",            "5:30", "min/km"),
        new ActivityMetricDto("duration",        "28:36","min"),
        new ActivityMetricDto("calories_burned", "320",  "kcal")
    ];

    public AnalyzeRunImageCommandHandlerTests()
    {
        _handler = new AnalyzeRunImageCommandHandler(_serviceMock.Object);
    }

    [Fact]
    public async Task Handle_BuildsFormattedActivityText()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThreeMetrics);

        var result = await _handler.Handle(new AnalyzeRunImageCommand([1], "image/jpeg"), CancellationToken.None);

        result.ActivityText.Should().Be(
            "I run for 5.2km at a pace of 5:30min/km. Total time: 28:36min.");
    }

    [Fact]
    public async Task Handle_WhenCaloriesBurnedPresent_ExtractsCalories()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FourMetrics);

        var result = await _handler.Handle(new AnalyzeRunImageCommand([1], "image/jpeg"), CancellationToken.None);

        result.CaloriesBurned.Should().Be(320.0);
    }

    [Fact]
    public async Task Handle_WhenCaloriesBurnedAbsent_DefaultsToZero()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThreeMetrics);

        var result = await _handler.Handle(new AnalyzeRunImageCommand([1], "image/jpeg"), CancellationToken.None);

        result.CaloriesBurned.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_ReturnsOriginalMetrics()
    {
        _serviceMock
            .Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FourMetrics);

        var result = await _handler.Handle(new AnalyzeRunImageCommand([1], "image/jpeg"), CancellationToken.None);

        result.Metrics.Should().BeEquivalentTo(FourMetrics);
    }
}
