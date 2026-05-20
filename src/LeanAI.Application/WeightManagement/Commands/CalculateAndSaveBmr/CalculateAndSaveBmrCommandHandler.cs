using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.CalculateAndSaveBmr;

public sealed class CalculateAndSaveBmrCommandHandler(
    IAppSettingsRepository      settingsRepository,
    IUserProfileRepository      profileRepository,
    ICaloryLogRepository        caloryLogRepository)
    : IRequestHandler<CalculateAndSaveBmrCommand>
{
    public async Task Handle(CalculateAndSaveBmrCommand request, CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        if (settings is null || !settings.UseBmr) return;

        var profile = await profileRepository.GetAsync(cancellationToken);
        if (profile?.HeightCm is null || profile.Gender is null || profile.Age is null) return;

        var bmr = (10 * request.WeightKg)
                + (6.25 * profile.HeightCm.Value)
                - (5    * profile.Age.Value)
                + (profile.Gender.Value == Gender.Male ? -5 : -161);

        await caloryLogRepository.UpsertBmrAsync(request.Date, bmr, cancellationToken);
    }
}
