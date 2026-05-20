using LeanAI.Domain.Common;

namespace LeanAI.Domain.FoodTracking.Entities;

public class CaloryLog : BaseEntity
{
    public DateOnly Date       { get; set; }
    public double   Calories   { get; set; }
    public string   SourceType  { get; set; } = string.Empty;
    public string?  Description { get; set; }
}
