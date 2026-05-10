using LeanAI.Domain.Common;

namespace LeanAI.Domain.WeightManagement.Entities;

public class DailyIdealWeight : BaseEntity
{
    public DateOnly Date     { get; set; }
    public double   WeightKg { get; set; }
}
