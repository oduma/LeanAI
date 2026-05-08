using CommunityToolkit.Maui;
using LeanAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        builder.Services.AddDbContext<LeanAIDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyReference).Assembly));

        builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(Application.AssemblyReference)));

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
