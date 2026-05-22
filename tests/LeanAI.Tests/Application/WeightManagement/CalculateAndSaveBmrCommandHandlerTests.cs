using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.CalculateAndSaveBmr;
using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class CalculateAndSaveBmrCommandHandlerTests
{
    private readonly Mock<IAppSettingsRepository> _settingsRepoMock  = new();
    private readonly Mock<IUserProfileRepository> _profileRepoMock   = new();
    private readonly Mock<IEnergyLogRepository>   _energyLogRepoMock = new();
    private readonly CalculateAndSaveBmrCommandHandler _handler;

    private static readonly DateOnly TestDate   = new(2026, 5, 20);
    private static readonly double   TestWeight = 80.0;

    public CalculateAndSaveBmrCommandHandlerTests()
    {
        _handler = new CalculateAndSaveBmrCommandHandler(
            _settingsRepoMock.Object,
            _profileRepoMock.Object,
            _energyLogRepoMock.Object);

        _energyLogRepoMock
            .Setup(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupSettings(bool useBmr) =>
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { UseBmr = useBmr });

    private void SetupFullProfile(Gender gender) =>
        _profileRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { Gender = gender, HeightCm = 175.0, Age = 30 });

    [Fact]
    public async Task Handle_UseBmrFalse_IsNoOp()
    {
        SetupSettings(useBmr: false);

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullSettings_IsNoOp()
    {
        _settingsRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSettings?)null);

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProfileMissingHeight_IsNoOp()
    {
        SetupSettings(useBmr: true);
        _profileRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { Gender = Gender.Male, HeightCm = null, Age = 30 });

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProfileMissingGender_IsNoOp()
    {
        SetupSettings(useBmr: true);
        _profileRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { Gender = null, HeightCm = 175.0, Age = 30 });

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProfileMissingAge_IsNoOp()
    {
        SetupSettings(useBmr: true);
        _profileRepoMock
            .Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { Gender = Gender.Female, HeightCm = 175.0, Age = null });

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Male_ComputesCorrectBmrAndUpserts()
    {
        // BMR = (10×80) + (6.25×175) − (5×30) − 5 = 800 + 1093.75 − 150 − 5 = 1738.75
        SetupSettings(useBmr: true);
        SetupFullProfile(Gender.Male);

        double? capturedCalories = null;
        _energyLogRepoMock
            .Setup(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Callback<DateOnly, double, CancellationToken>((_, cal, _) => capturedCalories = cal)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        capturedCalories.Should().BeApproximately(1738.75, precision: 0.01);
        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(TestDate, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Female_ComputesCorrectBmrAndUpserts()
    {
        // BMR = (10×80) + (6.25×175) − (5×30) − 161 = 800 + 1093.75 − 150 − 161 = 1582.75
        SetupSettings(useBmr: true);
        SetupFullProfile(Gender.Female);

        double? capturedCalories = null;
        _energyLogRepoMock
            .Setup(r => r.UpsertBmrAsync(It.IsAny<DateOnly>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Callback<DateOnly, double, CancellationToken>((_, cal, _) => capturedCalories = cal)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new CalculateAndSaveBmrCommand(TestDate, TestWeight), CancellationToken.None);

        capturedCalories.Should().BeApproximately(1582.75, precision: 0.01);
        _energyLogRepoMock.Verify(r => r.UpsertBmrAsync(TestDate, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
