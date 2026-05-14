using LeanAI.Domain.Common;

namespace LeanAI.Domain.WeightManagement.Entities;

public class WeeklyAverage : BaseEntity
{
    /// <summary>Always the Monday of the Mon–Sun week.</summary>
    public DateOnly WeekStart       { get; set; }
    public double   AverageWeightKg { get; set; }
}
