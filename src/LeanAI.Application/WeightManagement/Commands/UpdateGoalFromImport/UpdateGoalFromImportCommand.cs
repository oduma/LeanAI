using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.UpdateGoalFromImport;

public sealed record UpdateGoalFromImportCommand(
    DateOnly GoalStartDate,
    DateOnly GoalEndDate,
    double   StartingWeightKg
) : IRequest;
