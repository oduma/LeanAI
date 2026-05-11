using LeanAI.Domain.WeightManagement.Enums;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.GenerateIdealPath;

public record GenerateIdealPathCommand(
    DateOnly     StartDate,
    double       StartingWeightKg,
    double       TargetWeightKg,
    TargetPeriod TargetPeriod,
    int?         ExactTotalDays = null
) : IRequest<Unit>;
