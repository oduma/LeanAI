using LeanAI.Domain.Common;

namespace LeanAI.Domain.Routine.Entities;

public class RoutineFoodItem : BaseEntity
{
    public string  Description { get; set; } = string.Empty;
    public string? Quantity    { get; set; }
    public double  Calories    { get; set; }
}
