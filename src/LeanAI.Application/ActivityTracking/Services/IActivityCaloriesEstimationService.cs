namespace LeanAI.Application.ActivityTracking.Services;

public interface IActivityCaloriesEstimationService
{
    Task<IReadOnlyList<double>> EstimateAsync(
        IReadOnlyList<string> descriptions,
        CancellationToken ct = default);
}
