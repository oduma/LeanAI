using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeanAI.Infrastructure.Repositories;

public class AppSettingsRepository(LeanAIDbContext context) : IAppSettingsRepository
{
    public Task<AppSettings?> GetAsync(CancellationToken ct = default) =>
        context.AppSettings.FirstOrDefaultAsync(ct);

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var tracked = context.ChangeTracker.Entries<AppSettings>()
            .Any(e => e.Entity.Id == settings.Id);

        if (!tracked)
        {
            var exists = await context.AppSettings
                .AsNoTracking()
                .AnyAsync(s => s.Id == settings.Id, ct);

            if (exists)
                context.AppSettings.Update(settings);
            else
                context.AppSettings.Add(settings);
        }

        await context.SaveChangesAsync(ct);
    }
}
