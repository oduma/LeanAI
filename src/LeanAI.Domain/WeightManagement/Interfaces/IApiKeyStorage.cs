namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IApiKeyStorage
{
    Task<string?> GetAsync(string keyName, CancellationToken ct = default);
    Task SetAsync(string keyName, string value, CancellationToken ct = default);
}
