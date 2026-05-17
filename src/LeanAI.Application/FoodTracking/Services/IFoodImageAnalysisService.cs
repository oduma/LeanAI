using LeanAI.Application.FoodTracking.DTOs;

namespace LeanAI.Application.FoodTracking.Services;

public interface IFoodImageAnalysisService
{
    Task<IReadOnlyList<FoodItemDto>> AnalyzeImageAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
    Task<IReadOnlyList<FoodItemDto>> RecalculateCaloriesAsync(IReadOnlyList<FoodItemInputDto> items, CancellationToken ct = default);
}
