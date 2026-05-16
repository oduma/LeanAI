using LeanAI.Application.ActivityTracking.DTOs;

namespace LeanAI.Application.ActivityTracking.Services;

public interface IRunImageAnalysisService
{
    Task<IReadOnlyList<ActivityMetricDto>> AnalyzeAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
