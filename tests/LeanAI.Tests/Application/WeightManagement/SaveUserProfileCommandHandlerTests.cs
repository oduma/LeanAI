using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.SaveUserProfile;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class SaveUserProfileCommandHandlerTests
{
    private readonly Mock<IUserProfileRepository> _repositoryMock = new();
    private readonly SaveUserProfileCommandHandler _handler;

    public SaveUserProfileCommandHandlerTests()
    {
        _handler = new SaveUserProfileCommandHandler(_repositoryMock.Object);
    }

    private static SaveUserProfileCommand FullCommand() => new(
        UnitSystem:       UnitSystem.Metric,
        Gender:           Gender.Female,
        Age:              28,
        HeightCm:         165.0,
        StartingWeightKg: 70.0,
        TargetWeightKg:   60.0,
        TargetPeriod:     TargetPeriod.SixMonths
    );

    [Fact]
    public async Task Handle_WhenNoExistingProfile_CreatesNewProfileAndSaves()
    {
        _repositoryMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        UserProfile? saved = null;
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        var result = await _handler.Handle(FullCommand(), CancellationToken.None);

        result.Should().Be(Unit.Value);
        saved.Should().NotBeNull();
        saved!.Gender.Should().Be(Gender.Female);
        saved.Age.Should().Be(28);
        saved.HeightCm.Should().Be(165.0);
        saved.StartingWeightKg.Should().Be(70.0);
        saved.TargetWeightKg.Should().Be(60.0);
        saved.TargetPeriod.Should().Be(TargetPeriod.SixMonths);
        saved.UnitSystem.Should().Be(UnitSystem.Metric);
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProfileExists_UpdatesExistingProfileAndSaves()
    {
        var existing = new UserProfile { Gender = Gender.Male, Age = 40 };
        _repositoryMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        UserProfile? saved = null;
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        await _handler.Handle(FullCommand(), CancellationToken.None);

        saved.Should().BeSameAs(existing);
        saved!.Gender.Should().Be(Gender.Female);
        saved.Age.Should().Be(28);
        _repositoryMock.Verify(r => r.SaveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPartialCommand_SavesPartialProfile()
    {
        _repositoryMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        var command = new SaveUserProfileCommand(
            UnitSystem:       UnitSystem.Imperial,
            Gender:           null,
            Age:              null,
            HeightCm:         null,
            StartingWeightKg: null,
            TargetWeightKg:   null,
            TargetPeriod:     null
        );

        UserProfile? saved = null;
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => saved = p);

        await _handler.Handle(command, CancellationToken.None);

        saved!.UnitSystem.Should().Be(UnitSystem.Imperial);
        saved.IsComplete.Should().BeFalse();
    }
}
