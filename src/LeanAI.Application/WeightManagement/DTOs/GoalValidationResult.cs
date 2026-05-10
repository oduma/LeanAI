using LeanAI.Application.WeightManagement.Enums;

namespace LeanAI.Application.WeightManagement.DTOs;

public record GoalValidationResult(GoalValidationStatus Status, string Message);
