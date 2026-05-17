using LeanAI.Domain.Common;

namespace LeanAI.Domain.FoodTracking.Entities;

public class FoodLog : BaseEntity
{
    public DateOnly  Date        { get; set; }
    public string    FoodItem    { get; set; } = string.Empty;
    public string    Quantity    { get; set; } = string.Empty;
    public Guid      CaloryLogId { get; set; }
    public CaloryLog CaloryLog   { get; set; } = null!;
}
