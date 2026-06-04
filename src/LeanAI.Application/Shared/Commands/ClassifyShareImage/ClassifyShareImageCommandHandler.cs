using LeanAI.Application.Shared.Services;
using MediatR;

namespace LeanAI.Application.Shared.Commands.ClassifyShareImage;

public sealed class ClassifyShareImageCommandHandler(IShareImageClassificationService classifier)
    : IRequestHandler<ClassifyShareImageCommand, ShareImageType>
{
    public Task<ShareImageType> Handle(ClassifyShareImageCommand request, CancellationToken ct)
        => classifier.ClassifyAsync(request.ImageBytes, request.MimeType, ct);
}
