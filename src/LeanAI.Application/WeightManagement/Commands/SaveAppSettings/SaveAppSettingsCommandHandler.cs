using LeanAI.Application.Common;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.SaveAppSettings;

public sealed class SaveAppSettingsCommandHandler(
    IAppSettingsRepository settingsRepository,
    IApiKeyStorage apiKeyStorage)
    : IRequestHandler<SaveAppSettingsCommand, Unit>
{
    public async Task<Unit> Handle(SaveAppSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken) ?? new AppSettings();
        settings.GeminiModelName = request.GeminiModelName;
        await settingsRepository.SaveAsync(settings, cancellationToken);
        await apiKeyStorage.SetAsync(ApiKeyNames.Gemini, request.GeminiApiKey, cancellationToken);
        return Unit.Value;
    }
}
