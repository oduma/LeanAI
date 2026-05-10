using LeanAI.Domain.Common;
using LeanAI.Domain.WeightManagement.Enums;

namespace LeanAI.Domain.WeightManagement.Entities;

public class UserProfile : BaseEntity
{
    public UnitSystem UnitSystem { get; set; } = UnitSystem.Metric;
    public Gender? Gender { get; set; }
    public int? Age { get; set; }
    public double? HeightCm { get; set; }
    public double? StartingWeightKg { get; set; }
    public double? TargetWeightKg { get; set; }
    public TargetPeriod? TargetPeriod { get; set; }

    public bool IsComplete =>
        Gender.HasValue &&
        Age.HasValue &&
        HeightCm.HasValue &&
        StartingWeightKg.HasValue &&
        TargetWeightKg.HasValue &&
        TargetPeriod.HasValue;
}
