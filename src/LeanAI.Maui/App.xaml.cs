using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using LeanAI.Infrastructure;
using LeanAI.Maui.Views.Wizard;
using MediatR;

namespace LeanAI.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _services;

    // Bootstrap value stored in SecureStorage on first launch only.
    // On subsequent launches the value comes from SecureStorage (Android Keystore).
    private const string GeminiKeyStorageKey = "gemini_key";
    private const string GeminiBootstrapKey  = "AIzaSyB89Doqu3Pekm74qqxjjVyxGR8NdUfgTEA";

    public App(IMediator mediator, IServiceProvider services)
    {
        InitializeComponent();
        _mediator = mediator;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Created += async (_, _) =>
        {
            await ProvisionAiSettingsAsync();
            await ShowWizardIfNeededAsync();
        };
        return window;
    }

    private async Task ProvisionAiSettingsAsync()
    {
        // Provision API key
        var key = await SecureStorage.GetAsync(GeminiKeyStorageKey);
        if (string.IsNullOrEmpty(key))
        {
            key = GeminiBootstrapKey;
            await SecureStorage.SetAsync(GeminiKeyStorageKey, key);
        }
        _services.SetGeminiApiKey(key);

        // Load model name from DB (uses default if no AppSettings row exists yet)
        using var scope = _services.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
        var settings = await settingsRepo.GetAsync();
        _services.SetGeminiModelName(settings?.GeminiModelName ?? AppSettings.DefaultModelName);
    }

    private async Task ShowWizardIfNeededAsync()
    {
        var profile = await _mediator.Send(new GetUserProfileQuery());
        if (profile is null || !profile.IsComplete)
        {
            var wizardPage = _services.GetRequiredService<WizardPage>();
            await Windows[0].Page!.Navigation.PushModalAsync(wizardPage);
        }
    }
}
