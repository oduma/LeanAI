using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LeanAI.Application.WeightManagement.Services;

namespace LeanAI.Infrastructure.Services;

public class GoogleOAuthService(HttpClient http, IGoogleTokenStorage tokenStorage)
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public Task<string?> GetRefreshTokenAsync() =>
        tokenStorage.GetRefreshTokenAsync();

    public Task SetRefreshTokenAsync(string token) =>
        tokenStorage.SetRefreshTokenAsync(token);

    public Task ClearRefreshTokenAsync() =>
        tokenStorage.ClearRefreshTokenAsync();

    public async Task<TokenResponse> ExchangeAuthCodeAsync(
        string clientId, string redirectUri, string code, string codeVerifier)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = clientId,
            ["redirect_uri"]  = redirectUri,
            ["code"]          = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"]    = "authorization_code"
        });

        var response = await http.PostAsync(TokenEndpoint, content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public async Task<string> RefreshAccessTokenAsync(string clientId, string refreshToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = clientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"]    = "refresh_token"
        });

        var response = await http.PostAsync(TokenEndpoint, content);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        return result.AccessToken;
    }
}

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")]  string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")]    int ExpiresIn
);
