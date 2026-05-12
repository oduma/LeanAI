namespace LeanAI.Application.WeightManagement.DTOs;

/// <param name="WeekStart">The Monday (or GoalEndDate) this bar is anchored to.</param>
/// <param name="DeltaKg">avg(prevWeek) − avg(thisWeek). Positive = lost weight.</param>
public sealed record WeeklyDeltaDto(DateOnly WeekStart, double DeltaKg);
