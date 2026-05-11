using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.SaveAppSettings;

public sealed record SaveAppSettingsCommand(
    string GeminiModelName,
    string GeminiApiKey) : IRequest<Unit>;
