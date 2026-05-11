using LeanAI.Domain.WeightManagement.Enums;

namespace LeanAI.Application.WeightManagement.DTOs;

public sealed record LogContextDto(
    double?    TodayWeightKg,
    string?    TodayNotes,
    double?    YesterdayWeightKg,
    double?    TodayIdealWeightKg,
    double?    WeekFirstWeightKg,
    int        WeekDaysLogged,
    double?    IdealWeeklyLossKg,
    UnitSystem UnitSystem
);
