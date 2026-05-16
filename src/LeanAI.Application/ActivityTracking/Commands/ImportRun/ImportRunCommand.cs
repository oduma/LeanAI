using LeanAI.Application.ActivityTracking.DTOs;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.ImportRun;

public sealed record ImportRunCommand(byte[] ImageBytes, string MimeType, DateOnly Date)
    : IRequest<IReadOnlyList<ActivityMetricDto>>;
