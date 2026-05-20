using LeanAI.Application.ActivityTracking.DTOs;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.AnalyzeRunImage;

public sealed record AnalyzeRunImageCommand(byte[] ImageBytes, string MimeType)
    : IRequest<RunImportResultDto>;
