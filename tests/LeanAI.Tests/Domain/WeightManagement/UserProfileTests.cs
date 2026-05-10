using FluentAssertions;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;

namespace LeanAI.Tests.Domain.WeightManagement;

public class UserProfileTests
{
    private static UserProfile FullProfile() => new()
    {
        Gender          = Gender.Male,
        Age             = 30,
        HeightCm        = 175.0,
        StartingWeightKg = 90.0,
        TargetWeightKg  = 80.0,
        TargetPeriod    = TargetPeriod.ThreeMonths
    };

    [Fact]
    public void IsComplete_WhenAllFieldsSet_ReturnsTrue()
    {
        var profile = FullProfile();
        profile.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_WhenGenderMissing_ReturnsFalse()
    {
        var profile = FullProfile();
        profile.Gender = null;
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_WhenAgeMissing_ReturnsFalse()
    {
        var profile = FullProfile();
        profile.Age = null;
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_WhenHeightMissing_ReturnsFalse()
    {
        var profile = FullProfile();
        profile.HeightCm = null;
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_WhenStartingWeightMissing_ReturnsFalse()
    {
        var profile = FullProfile();
        profile.StartingWeightKg = null;
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_WhenTargetWeightMissing_ReturnsFalse()
    {
        var profile = FullProfile();
        profile.TargetWeightKg = null;
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_WhenTargetPeriodMissing_ReturnsFalse()
    {
        var profile = FullProfile();
        profile.TargetPeriod = null;
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_WhenAllFieldsNull_ReturnsFalse()
    {
        var profile = new UserProfile();
        profile.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void UnitSystem_DefaultsToMetric()
    {
        var profile = new UserProfile();
        profile.UnitSystem.Should().Be(UnitSystem.Metric);
    }

    [Fact]
    public void Id_IsAssignedOnCreation()
    {
        var profile = new UserProfile();
        profile.Id.Should().NotBe(Guid.Empty);
    }
}
