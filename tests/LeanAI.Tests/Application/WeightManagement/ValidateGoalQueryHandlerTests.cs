using FluentAssertions;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Enums;
using LeanAI.Application.WeightManagement.Queries.ValidateGoal;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.WeightManagement.Enums;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class ValidateGoalQueryHandlerTests
{
    private readonly Mock<IAIGoalValidationService> _serviceMock = new();
    private readonly ValidateGoalQueryHandler _handler;

    private static readonly ValidateGoalQuery SampleQuery = new(
        Gender: Gender.Male,
        Age: 35,
        HeightCm: 180.0,
        StartingWeightKg: 90.0,
        TargetWeightKg: 80.0,
        TargetPeriod: TargetPeriod.ThreeMonths);

    public ValidateGoalQueryHandlerTests()
    {
        _handler = new ValidateGoalQueryHandler(_serviceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsSafe_ReturnsSafeResult()
    {
        var expected = new GoalValidationResult(GoalValidationStatus.Safe, "Your goal is achievable.");
        _serviceMock
            .Setup(s => s.ValidateAsync(SampleQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(SampleQuery, CancellationToken.None);

        result.Should().Be(expected);
        result.Status.Should().Be(GoalValidationStatus.Safe);
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsWarning_ReturnsWarningResult()
    {
        var expected = new GoalValidationResult(GoalValidationStatus.Warning, "Aggressive goal — proceed with caution.");
        _serviceMock
            .Setup(s => s.ValidateAsync(SampleQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(SampleQuery, CancellationToken.None);

        result.Should().Be(expected);
        result.Status.Should().Be(GoalValidationStatus.Warning);
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsDanger_ReturnsDangerResult()
    {
        var expected = new GoalValidationResult(GoalValidationStatus.Danger, "This goal is not medically safe.");
        _serviceMock
            .Setup(s => s.ValidateAsync(SampleQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(SampleQuery, CancellationToken.None);

        result.Should().Be(expected);
        result.Status.Should().Be(GoalValidationStatus.Danger);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        _serviceMock
            .Setup(s => s.ValidateAsync(SampleQuery, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Gemini API error"));

        var act = async () => await _handler.Handle(SampleQuery, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Gemini API error");
    }
}
