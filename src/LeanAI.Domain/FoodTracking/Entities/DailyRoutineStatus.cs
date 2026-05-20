using LeanAI.Domain.Common;

namespace LeanAI.Domain.FoodTracking.Entities;

public class DailyRoutineStatus : BaseEntity
{
    public DateOnly Date     { get; set; }
    public bool     IsActive { get; set; }
}
