using LeanAI.Domain.Common;

namespace LeanAI.Domain.ActivityTracking.Entities;

public class CustomActivityLog : BaseEntity
{
    public DateOnly Date        { get; set; }
    public string   Description { get; set; } = string.Empty;
    public Guid     EnergyLogId { get; set; }
}
