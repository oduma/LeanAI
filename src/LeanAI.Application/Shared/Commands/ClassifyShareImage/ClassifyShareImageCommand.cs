using LeanAI.Application.Shared;
using MediatR;

namespace LeanAI.Application.Shared.Commands.ClassifyShareImage;

public record ClassifyShareImageCommand(byte[] ImageBytes, string MimeType)
    : IRequest<ShareImageType>;
