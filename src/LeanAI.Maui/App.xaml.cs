using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Maui.Views.Wizard;
using MediatR;

namespace LeanAI.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _services;

    public App(IMediator mediator, IServiceProvider services)
    {
        InitializeComponent();
        _mediator = mediator;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Created += async (_, _) => await ShowWizardIfNeededAsync();
        return window;
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
