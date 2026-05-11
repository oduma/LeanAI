using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.UpdateGoalFromImport;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class UpdateGoalFromImportCommandHandlerTests
{
    private readonly Mock<IUserProfileRepository> _profileRepoMock = new();
    private readonly UpdateGoalFromImportCommandHandler _handler;

    private static readonly DateOnly Start = new(2025, 1, 15);
    private static readonly DateOnly End   = new(2026, 1, 15);

    public UpdateGoalFromImportCommandHandlerTests()
    {
        _handler = new UpdateGoalFromImportCommandHandler(_profileRepoMock.Object);
    }

    [Fact]
    public async Task Handle_SetsGoalDatesAndStartingWeight_OnExistingProfile()
    {
        var profile = new UserProfile
        {
            UnitSystem       = UnitSystem.Metric,
            Gender           = Gender.Male,
            Age              = 30,
            HeightCm         = 180,
            StartingWeightKg = 90.0,
            TargetWeightKg   = 75.0,
            TargetPeriod     = TargetPeriod.OneYear
        };

        _profileRepoMock.Setup(r => r.GetAsync()).ReturnsAsync(profile);
        _profileRepoMock.Setup(r => r.SaveAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);

        var cmd = new UpdateGoalFromImportCommand(Start, End, 92.5);
        await _handler.Handle(cmd, CancellationToken.None);

        profile.GoalStartDate.Should().Be(Start);
        profile.GoalEndDate.Should().Be(End);
        profile.StartingWeightKg.Should().Be(92.5);

        _profileRepoMock.Verify(r => r.SaveAsync(profile), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoProfileExists()
    {
        _profileRepoMock.Setup(r => r.GetAsync()).ReturnsAsync((UserProfile?)null);

        var cmd = new UpdateGoalFromImportCommand(Start, End, 92.5);
        await _handler.Handle(cmd, CancellationToken.None);

        _profileRepoMock.Verify(r => r.SaveAsync(It.IsAny<UserProfile>()), Times.Never);
    }
}
