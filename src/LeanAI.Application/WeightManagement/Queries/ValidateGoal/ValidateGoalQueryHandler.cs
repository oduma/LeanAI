using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Services;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.ValidateGoal;

public class ValidateGoalQueryHandler(IAIGoalValidationService validationService)
    : IRequestHandler<ValidateGoalQuery, GoalValidationResult>
{
    public Task<GoalValidationResult> Handle(ValidateGoalQuery request, CancellationToken cancellationToken)
        => validationService.ValidateAsync(request, cancellationToken);
}
