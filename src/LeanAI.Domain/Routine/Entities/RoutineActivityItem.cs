using LeanAI.Domain.Common;

namespace LeanAI.Domain.Routine.Entities;

public class RoutineActivityItem : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public double Calories    { get; set; }
}
