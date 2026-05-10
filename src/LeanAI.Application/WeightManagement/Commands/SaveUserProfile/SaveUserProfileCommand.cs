using LeanAI.Domain.WeightManagement.Enums;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.SaveUserProfile;

public record SaveUserProfileCommand(
    UnitSystem    UnitSystem,
    Gender?       Gender,
    int?          Age,
    double?       HeightCm,
    double?       StartingWeightKg,
    double?       TargetWeightKg,
    TargetPeriod? TargetPeriod
) : IRequest<Unit>;
