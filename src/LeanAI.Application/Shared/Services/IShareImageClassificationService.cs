namespace LeanAI.Application.Shared.Services;

public interface IShareImageClassificationService
{
    Task<ShareImageType> ClassifyAsync(
        byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
