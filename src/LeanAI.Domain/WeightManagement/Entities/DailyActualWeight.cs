using LeanAI.Domain.Common;

namespace LeanAI.Domain.WeightManagement.Entities;

public class DailyActualWeight : BaseEntity
{
    public DateOnly Date     { get; set; }
    public double   WeightKg { get; set; }
    public string?  Notes    { get; set; }
}
