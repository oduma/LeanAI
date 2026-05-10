using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Queries.ValidateGoal;

namespace LeanAI.Application.WeightManagement.Services;

public interface IAIGoalValidationService
{
    Task<GoalValidationResult> ValidateAsync(ValidateGoalQuery query, CancellationToken ct = default);
}
