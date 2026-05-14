namespace LeanAI.Application.WeightManagement.DTOs;

public sealed record CalendarMonthDto(
    int                           Year,
    int                           Month,
    DateOnly                      GoalStart,
    DateOnly                      GoalEnd,
    DayOfWeek                     FirstDayOfWeek,
    IReadOnlyList<CalendarDayDto> Days);
