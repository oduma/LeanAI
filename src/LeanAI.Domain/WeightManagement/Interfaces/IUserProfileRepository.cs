using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Domain.WeightManagement.Interfaces;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(UserProfile profile, CancellationToken ct = default);
    Task DeleteAsync(CancellationToken ct = default);
}
