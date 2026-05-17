using LeanAI.Application.ActivityTracking.Services;
using LeanAI.Application.FoodTracking.Services;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.ActivityTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Interfaces;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure.ActivityTracking.Repositories;
using LeanAI.Infrastructure.ActivityTracking.Services;
using LeanAI.Infrastructure.FoodTracking.Repositories;
using LeanAI.Infrastructure.FoodTracking.Services;
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
        services.AddScoped<IDailyIdealWeightRepository, DailyIdealWeightRepository>();
        services.AddScoped<IDailyActualWeightRepository, DailyActualWeightRepository>();
        services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
        services.AddScoped<IWeeklyAverageRepository, WeeklyAverageRepository>();

        // Gemini AI — key and model name are populated by App.cs via SetGeminiApiKey/SetGeminiModelName()
        var keyHolder = new GeminiKeyHolder();
        services.AddSingleton(keyHolder);

        // Transient so each resolution picks up the current key and model name from the holder.
        services.AddTransient<IChatClient>(sp =>
        {
            var holder = sp.GetRequiredService<GeminiKeyHolder>();
            return new GeminiClient(holder.ApiKey).AsIChatClient(holder.ModelName);
        });

        services.AddTransient<IAIGoalValidationService, GeminiGoalValidationService>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddTransient<IRunImageAnalysisService, GeminiRunImageAnalysisService>();

        services.AddScoped<ICaloryLogRepository, CaloryLogRepository>();
        services.AddScoped<IFoodLogRepository, FoodLogRepository>();
        services.AddTransient<IFoodImageAnalysisService, GeminiFoodImageAnalysisService>();
        services.AddSingleton<FoodImportStateService>();

        services.AddHttpClient<GoogleOAuthService>();
        services.AddSingleton<IGoogleSheetsService, GoogleSheetsService>();

        return services;
    }

    /// <summary>
    /// Called by App.cs after reading the API key from SecureStorage.
    /// </summary>
    public static void SetGeminiApiKey(this IServiceProvider services, string apiKey)
        => services.GetRequiredService<GeminiKeyHolder>().ApiKey = apiKey;

    /// <summary>
    /// Called by App.cs after loading the model name from AppSettings.
    /// </summary>
    public static void SetGeminiModelName(this IServiceProvider services, string modelName)
        => services.GetRequiredService<GeminiKeyHolder>().ModelName = modelName;
}

/// <summary>
/// Carries the Gemini API key and model name from the MAUI presentation layer
/// to the Infrastructure IChatClient factory. Populated by App.cs before any AI call.
/// </summary>
public sealed class GeminiKeyHolder
{
    public string ApiKey   { get; set; } = string.Empty;
    public string ModelName { get; set; } = AppSettings.DefaultModelName;
}
