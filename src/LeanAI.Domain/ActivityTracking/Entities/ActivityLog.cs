using LeanAI.Domain.Common;

namespace LeanAI.Domain.ActivityTracking.Entities;

public class ActivityLog : BaseEntity
{
    public DateOnly Date          { get; set; }
    public string   Activity      { get; set; } = string.Empty;
    public string   ParameterName { get; set; } = string.Empty;
    public string   Value         { get; set; } = string.Empty;
    public string   Unit          { get; set; } = string.Empty;
}
