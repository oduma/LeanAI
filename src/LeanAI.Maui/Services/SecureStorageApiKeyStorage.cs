using LeanAI.Application.Common;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure;

namespace LeanAI.Maui.Services;

public sealed class SecureStorageApiKeyStorage(GeminiKeyHolder keyHolder) : IApiKeyStorage
{
    public async Task<string?> GetAsync(string keyName, CancellationToken ct = default) =>
        await SecureStorage.GetAsync(keyName);

    public async Task SetAsync(string keyName, string value, CancellationToken ct = default)
    {
        await SecureStorage.SetAsync(keyName, value);
        if (keyName == ApiKeyNames.Gemini)
            keyHolder.ApiKey = value;
    }
}
