using CommunityToolkit.Maui;
using LeanAI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using LeanAI.Infrastructure.Persistence;
using LeanAI.Maui.ViewModels;
using LeanAI.Maui.Views.Log;
using LeanAI.Maui.Views.Settings;
using LeanAI.Maui.Views.Trends;
using LeanAI.Maui.Views.Wizard;
using Microsoft.Extensions.Logging;

namespace LeanAI.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "leanai.db");
        builder.Services.AddInfrastructure(dbPath);

        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyReference).Assembly));

        builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(Application.AssemblyReference)));

        // Pages
        builder.Services.AddTransient<App>();
        builder.Services.AddTransient<WizardPage>();
        builder.Services.AddTransient<LogPage>();
        builder.Services.AddTransient<TrendsPage>();
        builder.Services.AddTransient<SettingsPage>();

        // ViewModels
        builder.Services.AddTransient<WizardViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LeanAIDbContext>();
            db.Database.Migrate();
        }

        return app;
    }
}
