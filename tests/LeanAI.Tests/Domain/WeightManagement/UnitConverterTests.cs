using FluentAssertions;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Services;

namespace LeanAI.Tests.Domain.WeightManagement;

public class UnitConverterTests
{
    private const double Tolerance = 0.001;

    // ── Kg ↔ Lb ───────────────────────────────────────────────────────────────

    [Fact]
    public void KgToLb_ConvertsCorrectly()
    {
        UnitConverter.KgToLb(1.0).Should().BeApproximately(2.20462, Tolerance);
    }

    [Fact]
    public void LbToKg_ConvertsCorrectly()
    {
        UnitConverter.LbToKg(1.0).Should().BeApproximately(0.45359, Tolerance);
    }

    [Fact]
    public void KgToLb_RoundTrip_ReturnsOriginalValue()
    {
        double original = 75.5;
        UnitConverter.LbToKg(UnitConverter.KgToLb(original))
            .Should().BeApproximately(original, Tolerance);
    }

    [Fact]
    public void KgToLb_Zero_ReturnsZero()
    {
        UnitConverter.KgToLb(0).Should().Be(0);
    }

    // ── Cm ↔ Inches ───────────────────────────────────────────────────────────

    [Fact]
    public void CmToInches_ConvertsCorrectly()
    {
        UnitConverter.CmToInches(2.54).Should().BeApproximately(1.0, Tolerance);
    }

    [Fact]
    public void InchesToCm_ConvertsCorrectly()
    {
        UnitConverter.InchesToCm(1.0).Should().BeApproximately(2.54, Tolerance);
    }

    [Fact]
    public void CmToInches_RoundTrip_ReturnsOriginalValue()
    {
        double original = 175.0;
        UnitConverter.InchesToCm(UnitConverter.CmToInches(original))
            .Should().BeApproximately(original, Tolerance);
    }

    [Fact]
    public void CmToInches_Zero_ReturnsZero()
    {
        UnitConverter.CmToInches(0).Should().Be(0);
    }

    // ── Feet + Inches ↔ Cm ────────────────────────────────────────────────────

    [Fact]
    public void FeetInchesToCm_ConvertsCorrectly()
    {
        // 5 ft 11 in = 180.34 cm
        UnitConverter.FeetInchesToCm(5, 11)
            .Should().BeApproximately(180.34, Tolerance);
    }

    [Fact]
    public void FeetInchesToCm_ZeroFeetZeroInches_ReturnsZero()
    {
        UnitConverter.FeetInchesToCm(0, 0).Should().Be(0);
    }

    [Fact]
    public void CmToFeetAndInches_ConvertsCorrectly()
    {
        var (feet, inches) = UnitConverter.CmToFeetAndInches(180.34);
        feet.Should().Be(5);
        inches.Should().BeApproximately(11.0, Tolerance);
    }

    [Fact]
    public void FeetInchesToCm_RoundTrip_ReturnsOriginalValues()
    {
        int originalFeet = 6;
        double originalInches = 2;
        double cm = UnitConverter.FeetInchesToCm(originalFeet, originalInches);
        var (feet, inches) = UnitConverter.CmToFeetAndInches(cm);
        feet.Should().Be(originalFeet);
        inches.Should().BeApproximately(originalInches, Tolerance);
    }

    // ── TargetPeriod.TotalDays ─────────────────────────────────────────────────

    [Theory]
    [InlineData(TargetPeriod.ThreeMonths, 90)]
    [InlineData(TargetPeriod.SixMonths,   180)]
    [InlineData(TargetPeriod.OneYear,     365)]
    public void TargetPeriod_TotalDays_ReturnsCorrectValue(
        TargetPeriod period, int expectedDays)
    {
        period.TotalDays().Should().Be(expectedDays);
    }
}
