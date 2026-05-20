using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.WeightManagement.Commands.DeleteUserProfile;
using LeanAI.Application.WeightManagement.Commands.SaveAppSettings;
using LeanAI.Application.WeightManagement.Commands.SaveUserProfile;
using LeanAI.Application.WeightManagement.Queries.GetAppSettings;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Maui.Messages;
using LeanAI.Maui.Views.Import;
using LeanAI.Maui.Views.Wizard;
using MediatR;

namespace LeanAI.Maui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IMediator           _mediator;
    private readonly IServiceProvider    _services;
    private readonly IGoogleTokenStorage _googleTokenStorage;

    private bool _isLoading;
    private CancellationTokenSource? _saveCts;

    [ObservableProperty] private string    _geminiModelName    = string.Empty;
    [ObservableProperty] private string    _geminiApiKey       = string.Empty;
    [ObservableProperty] private bool      _hasGoogleToken;
    [ObservableProperty] private int       _calendarFirstDayIndex;
    [ObservableProperty] private bool      _useBmr;

    public DayOfWeek CalendarFirstDay =>
        CalendarFirstDayIndex == 0 ? DayOfWeek.Monday : DayOfWeek.Sunday;

    public SettingsViewModel(IMediator mediator, IServiceProvider services, IGoogleTokenStorage googleTokenStorage)
    {
        _mediator           = mediator;
        _services           = services;
        _googleTokenStorage = googleTokenStorage;
    }

    partial void OnGeminiModelNameChanged(string value)
    {
        if (_isLoading) return;
        TriggerSave();
    }

    partial void OnGeminiApiKeyChanged(string value)
    {
        if (_isLoading) return;
        TriggerSave();
    }

    partial void OnCalendarFirstDayIndexChanged(int value)
    {
        if (_isLoading) return;
        _ = SaveImmediateAsync();
    }

    partial void OnUseBmrChanged(bool value)
    {
        if (_isLoading) return;
        _ = SaveImmediateAsync();
    }

    private void TriggerSave()
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        _ = SaveWithDebounceAsync(_saveCts.Token);
    }

    private async Task SaveWithDebounceAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(500, ct);
            await SaveImmediateAsync();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer keystroke — expected
        }
    }

    private async Task SaveImmediateAsync()
        => await _mediator.Send(new SaveAppSettingsCommand(GeminiModelName, GeminiApiKey, CalendarFirstDay, UseBmr));

    [RelayCommand]
    private async Task LoadAiSettingsAsync()
    {
        _isLoading = true;
        try
        {
            var dto = await _mediator.Send(new GetAppSettingsQuery());
            GeminiModelName       = dto.GeminiModelName;
            GeminiApiKey          = dto.GeminiApiKey;
            CalendarFirstDayIndex = dto.CalendarFirstDay == DayOfWeek.Monday ? 0 : 1;
            UseBmr                = dto.UseBmr;
            HasGoogleToken        = await _googleTokenStorage.GetRefreshTokenAsync() is not null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenImportWizardAsync()
    {
        var importPage = _services.GetRequiredService<ImportWizardPage>();
        await Shell.Current.Navigation.PushModalAsync(importPage);
        HasGoogleToken = await _googleTokenStorage.GetRefreshTokenAsync() is not null;
    }

    [RelayCommand]
    private async Task DisconnectGoogleAsync()
    {
        await _googleTokenStorage.ClearRefreshTokenAsync();
        HasGoogleToken = false;
    }

    [RelayCommand]
    private async Task ReRunSetupAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Re-Run the Setup?",
            "Your stats and goals will be permanently deleted.",
            "Confirm", "Cancel");

        if (!confirmed) return;

        var snapshot = await _mediator.Send(new GetUserProfileQuery());

        await _mediator.Send(new DeleteUserProfileCommand());

        var wizardPage = _services.GetRequiredService<WizardPage>();
        wizardPage.ViewModel.PrepareForRerun();

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
