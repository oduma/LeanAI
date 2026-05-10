using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Repositories;

public class UserProfileRepository(LeanAIDbContext context) : IUserProfileRepository
{
    public Task<UserProfile?> GetAsync(CancellationToken ct = default) =>
        context.UserProfiles.FirstOrDefaultAsync(ct);

    public async Task SaveAsync(UserProfile profile, CancellationToken ct = default)
    {
        var tracked = context.ChangeTracker.Entries<UserProfile>()
            .Any(e => e.Entity.Id == profile.Id);

        if (!tracked)
        {
            var exists = await context.UserProfiles
                .AsNoTracking()
                .AnyAsync(p => p.Id == profile.Id, ct);

            if (exists)
                context.UserProfiles.Update(profile);
            else
                context.UserProfiles.Add(profile);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
    {
        var profile = await context.UserProfiles.FirstOrDefaultAsync(ct);
        if (profile is not null)
        {
            context.UserProfiles.Remove(profile);
            await context.SaveChangesAsync(ct);
        }
    }
}
