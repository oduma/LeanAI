namespace LeanAI.Application.WeightManagement.DTOs;

public sealed record CalendarDayDto(
    DateOnly         Date,
    bool             IsInDisplayedMonth,
    CalendarDayState State,
    CalendarHaloColor Halo,
    CalendarTrendColor TextTrend,
    double?          WeightKg);
