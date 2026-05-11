using LeanAI.Application.Common;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetAppSettings;

public sealed class GetAppSettingsQueryHandler(
    IAppSettingsRepository settingsRepository,
    IApiKeyStorage apiKeyStorage)
    : IRequestHandler<GetAppSettingsQuery, AppSettingsDto>
{
    public async Task<AppSettingsDto> Handle(GetAppSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await settingsRepository.GetAsync(cancellationToken);
        var modelName = settings?.GeminiModelName ?? AppSettings.DefaultModelName;
        var apiKey = await apiKeyStorage.GetAsync(ApiKeyNames.Gemini, cancellationToken) ?? string.Empty;
        return new AppSettingsDto(modelName, apiKey);
    }
}
