using LeanAI.Application.WeightManagement.Services;

namespace LeanAI.Maui.Services;

public class SecureGoogleTokenStorage : IGoogleTokenStorage
{
    private const string Key = "google_refresh_token";

    public Task<string?> GetRefreshTokenAsync() => SecureStorage.GetAsync(Key);

    public Task SetRefreshTokenAsync(string token) => SecureStorage.SetAsync(Key, token);

    public Task ClearRefreshTokenAsync()
    {
        SecureStorage.Remove(Key);
        return Task.CompletedTask;
    }
}
