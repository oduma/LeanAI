using LeanAI.Domain.Common;

namespace LeanAI.Domain.FoodTracking.Entities;

public class RoutineItem : BaseEntity
{
    public string  SourceType  { get; set; } = string.Empty; // "food" | "activity"
    public string  Description { get; set; } = string.Empty;
    public string? Quantity    { get; set; }
    public double  Calories    { get; set; }
}
