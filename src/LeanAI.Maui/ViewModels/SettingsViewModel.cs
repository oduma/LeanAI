using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.WeightManagement.Commands.DeleteUserProfile;
using LeanAI.Application.WeightManagement.Commands.SaveAppSettings;
using LeanAI.Application.WeightManagement.Commands.SaveUserProfile;
using LeanAI.Application.WeightManagement.Queries.GetAppSettings;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Maui.Messages;
using LeanAI.Maui.Views.Wizard;
using MediatR;

namespace LeanAI.Maui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _services;

    private string _loadedModelName = string.Empty;
    private string _loadedApiKey    = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAiSettingsCommand))]
    private bool _hasChanges;

    [ObservableProperty] private string _geminiModelName = string.Empty;
    [ObservableProperty] private string _geminiApiKey    = string.Empty;

    public SettingsViewModel(IMediator mediator, IServiceProvider services)
    {
        _mediator = mediator;
        _services = services;
    }

    partial void OnGeminiModelNameChanged(string value) =>
        HasChanges = value != _loadedModelName || GeminiApiKey != _loadedApiKey;

    partial void OnGeminiApiKeyChanged(string value) =>
        HasChanges = GeminiModelName != _loadedModelName || value != _loadedApiKey;

    [RelayCommand]
    private async Task LoadAiSettingsAsync()
    {
        var dto = await _mediator.Send(new GetAppSettingsQuery());
        _loadedModelName = dto.GeminiModelName;
        _loadedApiKey    = dto.GeminiApiKey;
        GeminiModelName  = dto.GeminiModelName;
        GeminiApiKey     = dto.GeminiApiKey;
        HasChanges       = false;
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private async Task SaveAiSettingsAsync()
    {
        await _mediator.Send(new SaveAppSettingsCommand(GeminiModelName, GeminiApiKey));
        _loadedModelName = GeminiModelName;
        _loadedApiKey    = GeminiApiKey;
        HasChanges       = false;
    }

    [RelayCommand]
    private async Task ReRunSetupAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Re-Run the Setup?",
            "Your stats and goals will be permanently deleted.",
            "Confirm", "Cancel");

        if (!confirmed) return;

        // Snapshot before delete so we can restore on cancel
        var snapshot = await _mediator.Send(new GetUserProfileQuery());

        await _mediator.Send(new DeleteUserProfileCommand());

        var wizardPage = _services.GetRequiredService<WizardPage>();
        wizardPage.ViewModel.PrepareForRerun();

        // TaskCompletionSource bridges the modal lifecycle to this async method
        var tcs = new TaskCompletionSource<bool>();
        WeakReferenceMessenger.Default.Register<WizardDismissedMessage>(this, (_, m) =>
        {
            WeakReferenceMessenger.Default.Unregister<WizardDismissedMessage>(this);
            tcs.TrySetResult(m.Completed);
        });

        await Shell.Current.Navigation.PushModalAsync(wizardPage);
        bool completed = await tcs.Task;

        if (!completed && snapshot is not null)
        {
            await _mediator.Send(new SaveUserProfileCommand(
                UnitSystem:       snapshot.UnitSystem,
                Gender:           snapshot.Gender,
                Age:              snapshot.Age,
                HeightCm:         snapshot.HeightCm,
                StartingWeightKg: snapshot.StartingWeightKg,
                TargetWeightKg:   snapshot.TargetWeightKg,
                TargetPeriod:     snapshot.TargetPeriod
            ));
        }
    }
}
