using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Enums;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.ValidateGoal;

public record ValidateGoalQuery(
    Gender Gender,
    int Age,
    double HeightCm,
    double StartingWeightKg,
    double TargetWeightKg,
    TargetPeriod TargetPeriod
) : IRequest<GoalValidationResult>;
