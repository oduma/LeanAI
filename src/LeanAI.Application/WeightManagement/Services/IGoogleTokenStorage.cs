namespace LeanAI.Application.WeightManagement.Services;

public interface IGoogleTokenStorage
{
    Task<string?> GetRefreshTokenAsync();
    Task SetRefreshTokenAsync(string token);
    Task ClearRefreshTokenAsync();
}
