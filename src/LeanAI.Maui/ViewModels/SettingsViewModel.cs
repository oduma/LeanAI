using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.WeightManagement.Commands.DeleteUserProfile;
using LeanAI.Application.WeightManagement.Commands.SaveUserProfile;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Maui.Messages;
using LeanAI.Maui.Views.Wizard;
using MediatR;

namespace LeanAI.Maui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _services;

    public SettingsViewModel(IMediator mediator, IServiceProvider services)
    {
        _mediator = mediator;
        _services = services;
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
