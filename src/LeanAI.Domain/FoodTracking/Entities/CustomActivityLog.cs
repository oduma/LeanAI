using LeanAI.Domain.Common;

namespace LeanAI.Domain.FoodTracking.Entities;

public class CustomActivityLog : BaseEntity
{
    public DateOnly  Date        { get; set; }
    public string    Description { get; set; } = string.Empty;
    public Guid      CaloryLogId { get; set; }
    public CaloryLog CaloryLog   { get; set; } = null!;
}
