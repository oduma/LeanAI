using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IAppSettingsRepository
{
    Task<AppSettings?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
