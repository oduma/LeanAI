namespace LeanAI.Domain.WeightManagement.Enums;

public enum TargetPeriod
{
    ThreeMonths,
    SixMonths,
    OneYear
}

public static class TargetPeriodExtensions
{
    public static int TotalDays(this TargetPeriod period) => period switch
    {
        TargetPeriod.ThreeMonths => 90,
        TargetPeriod.SixMonths   => 180,
        TargetPeriod.OneYear     => 365,
        _                        => throw new ArgumentOutOfRangeException(nameof(period))
    };
}
