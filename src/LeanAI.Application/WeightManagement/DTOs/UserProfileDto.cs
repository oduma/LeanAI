using LeanAI.Domain.WeightManagement.Enums;

namespace LeanAI.Application.WeightManagement.DTOs;

public record UserProfileDto(
    Guid          Id,
    UnitSystem    UnitSystem,
    Gender?       Gender,
    int?          Age,
    double?       HeightCm,
    double?       StartingWeightKg,
    double?       TargetWeightKg,
    TargetPeriod? TargetPeriod,
    bool          IsComplete,
    DateOnly?     GoalStartDate
);
