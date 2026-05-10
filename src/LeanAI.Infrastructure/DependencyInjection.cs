using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.Persistence;
using LeanAI.Infrastructure.Repositories;
using LeanAI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Mscc.GenerativeAI.Microsoft;

namespace LeanAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string dbPath)
    {
        services.AddDbContext<LeanAIDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IUserProfileRepository, UserProfileRepository>();

        // Gemini AI — key is populated later by App.cs via SetGeminiApiKey()
        var keyHolder = new GeminiKeyHolder();
        services.AddSingleton(keyHolder);

        // IChatClient factory runs lazily on first resolution (first Step 3 validation),
        // by which time App.cs has already written the key into GeminiKeyHolder.
        services.AddSingleton<IChatClient>(sp =>
        {
            var holder = sp.GetRequiredService<GeminiKeyHolder>();
            return new GeminiClient(holder.ApiKey).AsIChatClient("gemini-2.5-flash");
        });

        services.AddTransient<IAIGoalValidationService, GeminiGoalValidationService>();

        return services;
    }

    /// <summary>
    /// Called by App.cs after reading the API key from SecureStorage.
    /// Populates the GeminiKeyHolder singleton so the IChatClient factory can use it.
    /// </summary>
    public static void SetGeminiApiKey(this IServiceProvider services, string apiKey)
        => services.GetRequiredService<GeminiKeyHolder>().ApiKey = apiKey;
}

/// <summary>
/// Carries the Gemini API key from the MAUI presentation layer to the Infrastructure IChatClient factory.
/// Populated by App.cs before any AI validation is requested.
/// </summary>
internal sealed class GeminiKeyHolder
{
    public string ApiKey { get; set; } = string.Empty;
}
