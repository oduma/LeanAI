using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.UpdateGoalFromImport;

public sealed class UpdateGoalFromImportCommandHandler(IUserProfileRepository profileRepository)
    : IRequestHandler<UpdateGoalFromImportCommand>
{
    public async Task Handle(UpdateGoalFromImportCommand request, CancellationToken cancellationToken)
    {
        var profile = await profileRepository.GetAsync(cancellationToken);
        if (profile is null) return;

        profile.GoalStartDate    = request.GoalStartDate;
        profile.GoalEndDate      = request.GoalEndDate;
        profile.StartingWeightKg = request.StartingWeightKg;

        await profileRepository.SaveAsync(profile, cancellationToken);
    }
}
