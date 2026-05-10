using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.SaveUserProfile;

public class SaveUserProfileCommandHandler(IUserProfileRepository repository)
    : IRequestHandler<SaveUserProfileCommand, Unit>
{
    public async Task<Unit> Handle(SaveUserProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetAsync(cancellationToken) ?? new UserProfile();

        profile.UnitSystem        = request.UnitSystem;
        profile.Gender            = request.Gender;
        profile.Age               = request.Age;
        profile.HeightCm          = request.HeightCm;
        profile.StartingWeightKg  = request.StartingWeightKg;
        profile.TargetWeightKg    = request.TargetWeightKg;
        profile.TargetPeriod      = request.TargetPeriod;

        await repository.SaveAsync(profile, cancellationToken);

        return Unit.Value;
    }
}
