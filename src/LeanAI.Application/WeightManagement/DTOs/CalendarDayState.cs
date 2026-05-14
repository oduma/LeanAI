namespace LeanAI.Application.WeightManagement.DTOs;

public enum CalendarDayState
{
    OutOfRange,      // Before GoalStartDate or after GoalEndDate
    FutureInRange,   // After today but within goal period — non-clickable
    PastNoRecord,    // On/before today, within goal period, no weight logged — clickable
    PastHasRecord,   // On/before today, within goal period, weight recorded — clickable
}
