using LeanAI.Domain.Common;

namespace LeanAI.Domain.EnergyTracking.Entities;

public class EnergyLog : BaseEntity
{
    public DateOnly Date          { get; set; }
    public double   Calories      { get; set; }
    public string   SourceType    { get; set; } = string.Empty;
    public string?  Description   { get; set; }
    public Guid?    RoutineItemId { get; set; }
}
