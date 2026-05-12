using FluentAssertions;
using LeanAI.Application.WeightManagement.Commands.GenerateIdealPath;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using Moq;

namespace LeanAI.Tests.Application.WeightManagement;

public class GenerateIdealPathCommandHandlerTests
{
    private readonly Mock<IDailyIdealWeightRepository> _repoMock    = new();
    private readonly Mock<IUserProfileRepository>      _profileMock = new();
    private readonly GenerateIdealPathCommandHandler   _handler;

    private static readonly DateOnly StartDate = new(2026, 5, 10);

    public GenerateIdealPathCommandHandlerTests()
    {
        _handler = new GenerateIdealPathCommandHandler(_repoMock.Object, _profileMock.Object);
    }

    [Fact]
    public async Task Handle_ThreeMonths_Generates90Entries()
    {
        IEnumerable<DailyIdealWeight>? captured = null;
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((entries, _) => captured = entries)
            .Returns(Task.CompletedTask);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        captured.Should().HaveCount(90);
    }

    [Fact]
    public async Task Handle_SixMonths_Generates180Entries()
    {
        IEnumerable<DailyIdealWeight>? captured = null;
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((entries, _) => captured = entries)
            .Returns(Task.CompletedTask);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.SixMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        captured.Should().HaveCount(180);
    }

    [Fact]
    public async Task Handle_OneYear_Generates365Entries()
    {
        IEnumerable<DailyIdealWeight>? captured = null;
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((entries, _) => captured = entries)
            .Returns(Task.CompletedTask);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.OneYear);
        await _handler.Handle(cmd, CancellationToken.None);

        captured.Should().HaveCount(365);
    }

    [Fact]
    public async Task Handle_Entries_HaveCorrectDates()
    {
        IReadOnlyList<DailyIdealWeight>? captured = null;
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((entries, _) => captured = entries.ToList())
            .Returns(Task.CompletedTask);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        captured![0].Date.Should().Be(StartDate);
        captured[89].Date.Should().Be(StartDate.AddDays(89));
    }

    [Fact]
    public async Task Handle_Entries_HaveCorrectWeights()
    {
        IReadOnlyList<DailyIdealWeight>? captured = null;
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((entries, _) => captured = entries.ToList())
            .Returns(Task.CompletedTask);

        // 90 kg → 75 kg over 90 days → DailyLoss = 15/90 = 1/6 kg/day
        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        var dailyLoss = (90.0 - 75.0) / 90;
        captured![0].WeightKg.Should().BeApproximately(90.0, 0.0001);
        captured[45].WeightKg.Should().BeApproximately(90.0 - dailyLoss * 45, 0.0001);
        captured[89].WeightKg.Should().BeApproximately(90.0 - dailyLoss * 89, 0.0001);
    }

    [Fact]
    public async Task Handle_ExactTotalDays_OverridesTargetPeriod()
    {
        IEnumerable<DailyIdealWeight>? captured = null;
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((entries, _) => captured = entries)
            .Returns(Task.CompletedTask);

        // 240 days: does not match any TargetPeriod enum value
        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths, ExactTotalDays: 240);
        await _handler.Handle(cmd, CancellationToken.None);

        captured.Should().HaveCount(240);
    }

    [Fact]
    public async Task Handle_DeleteAll_CalledBeforeInsert()
    {
        var callOrder = new List<string>();

        _repoMock
            .Setup(r => r.DeleteAllAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("delete"))
            .Returns(Task.CompletedTask);
        _repoMock
            .Setup(r => r.InsertBatchAsync(It.IsAny<IEnumerable<DailyIdealWeight>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DailyIdealWeight>, CancellationToken>((_, _) => callOrder.Add("insert"))
            .Returns(Task.CompletedTask);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        callOrder.Should().Equal("delete", "insert");
    }

    [Fact]
    public async Task Handle_SetsGoalStartDate_OnUserProfile()
    {
        var profile = new UserProfile();
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        profile.GoalStartDate.Should().Be(StartDate);
    }

    [Fact]
    public async Task Handle_SetsGoalEndDate_OnUserProfile()
    {
        var profile = new UserProfile();
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        await _handler.Handle(cmd, CancellationToken.None);

        // 90 days → StartDate + 89
        profile.GoalEndDate.Should().Be(StartDate.AddDays(89));
    }

    [Fact]
    public async Task Handle_WhenProfileIsNull_DoesNotThrow()
    {
        _profileMock.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((UserProfile?)null);

        var cmd = new GenerateIdealPathCommand(StartDate, 90.0, 75.0, TargetPeriod.ThreeMonths);
        var act = async () => await _handler.Handle(cmd, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
