using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.WeightManagement.Commands.SaveUserProfile;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Enums;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Application.WeightManagement.Queries.ValidateGoal;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Services;
using LeanAI.Maui.Messages;
using MediatR;

using DomainGender = LeanAI.Domain.WeightManagement.Enums.Gender;
using DomainTargetPeriod = LeanAI.Domain.WeightManagement.Enums.TargetPeriod;

namespace LeanAI.Maui.ViewModels;

public partial class WizardViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _validationCts;

    public WizardViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Step navigation ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsBackVisible))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    private int _currentStep = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBackVisible))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    private bool _isRerun;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    // Back button is visible on steps 2-3 always; on step 1 only during re-run
    public bool IsBackVisible => CurrentStep > 1 || IsRerun;

    // ── Step 3: AI goal validation ────────────────────────────────────────────

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationIconSource))]
    [NotifyPropertyChangedFor(nameof(ValidationTextColor))]
    [NotifyPropertyChangedFor(nameof(HasValidationResult))]
    [NotifyPropertyChangedFor(nameof(ShowAiDisclaimer))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private GoalValidationStatus? _validationStatus;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    public bool HasValidationResult => ValidationStatus.HasValue;

    public bool ShowAiDisclaimer =>
        ValidationStatus == GoalValidationStatus.Warning ||
        ValidationStatus == GoalValidationStatus.Danger;

    public string ValidationIconSource => ValidationStatus switch
    {
        GoalValidationStatus.Warning => "icon_warning.svg",
        GoalValidationStatus.Danger  => "icon_stop.svg",
        _                            => string.Empty
    };

    public Color ValidationTextColor => ValidationStatus switch
    {
        GoalValidationStatus.Warning => Color.FromArgb("#E8A838"),
        GoalValidationStatus.Danger  => Color.FromArgb("#C0392B"),
        _                            => Colors.Transparent
    };

    // ── Step 1: Unit system ───────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMetric))]
    [NotifyPropertyChangedFor(nameof(IsImperial))]
    [NotifyPropertyChangedFor(nameof(WeightUnitLabel))]
    [NotifyPropertyChangedFor(nameof(HeightUnitLabel))]
    private UnitSystem _unitSystem = UnitSystem.Metric;

    public bool IsMetric   => UnitSystem == UnitSystem.Metric;
    public bool IsImperial => UnitSystem == UnitSystem.Imperial;

    public string WeightUnitLabel => UnitSystem == UnitSystem.Metric ? "kg" : "lb";
    public string HeightUnitLabel => UnitSystem == UnitSystem.Metric ? "cm" : "ft / in";

    [RelayCommand]
    private void SelectMetric()   => UnitSystem = UnitSystem.Metric;

    [RelayCommand]
    private void SelectImperial() => UnitSystem = UnitSystem.Imperial;

    // ── Step 2: User stats ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private Gender? _gender;

    [RelayCommand]
    private void SelectMale()   => Gender = DomainGender.Male;

    [RelayCommand]
    private void SelectFemale() => Gender = DomainGender.Female;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _ageText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _heightCmText = string.Empty;

    // Imperial height — two fields combined into HeightCm on save
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _heightFeetText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _heightInchesText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _startingWeightText = string.Empty;

    // ── Step 3: Goal ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private string _targetWeightText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private TargetPeriod? _targetPeriod;

    [RelayCommand]
    private void SelectThreeMonths() => TargetPeriod = DomainTargetPeriod.ThreeMonths;

    [RelayCommand]
    private void SelectSixMonths()   => TargetPeriod = DomainTargetPeriod.SixMonths;

    [RelayCommand]
    private void SelectOneYear()     => TargetPeriod = DomainTargetPeriod.OneYear;

    // ── Navigation commands ───────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (IsRerun && CurrentStep == 1)
        {
            // Signal cancellation — WizardPage will pop the modal, SettingsViewModel will restore
            WeakReferenceMessenger.Default.Send(new WizardDismissedMessage(Completed: false));
            return;
        }
        CurrentStep--;
    }

    private bool CanGoBack() => CurrentStep > 1 || IsRerun;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task Next()
    {
        if (CurrentStep < 3)
        {
            await SaveCurrentStateAsync();
            CurrentStep++;
        }
        else
        {
            await SaveCurrentStateAsync();
            // Notify page to pop the modal (existing first-run path)
            WeakReferenceMessenger.Default.Send(new WizardCompletedMessage());
            // Notify SettingsViewModel that wizard completed successfully (re-run path)
            WeakReferenceMessenger.Default.Send(new WizardDismissedMessage(Completed: true));
        }
    }

    private bool CanGoNext() => CurrentStep switch
    {
        1 => true,
        2 => IsStep2Valid(),
        3 => IsStep3Valid() && ValidationStatus == GoalValidationStatus.Safe,
        _ => false
    };

    // ── Re-run mode ───────────────────────────────────────────────────────────

    public void PrepareForRerun()
    {
        IsRerun           = true;
        CurrentStep       = 1;
        UnitSystem        = UnitSystem.Metric;
        Gender            = null;
        AgeText           = string.Empty;
        HeightCmText      = string.Empty;
        HeightFeetText    = string.Empty;
        HeightInchesText  = string.Empty;
        StartingWeightText = string.Empty;
        TargetWeightText  = string.Empty;
        TargetPeriod      = null;
        ValidationStatus  = null;
        ValidationMessage = string.Empty;
    }

    // ── AI validation ─────────────────────────────────────────────────────────

    partial void OnTargetWeightTextChanged(string value) => TriggerValidationDebounce();
    partial void OnTargetPeriodChanged(TargetPeriod? value) => TriggerValidationDebounce();

    private void TriggerValidationDebounce()
    {
        if (CurrentStep != 3) return;

        _validationCts?.Cancel();
        ValidationStatus  = null;
        ValidationMessage = string.Empty;

        if (!IsStep3Valid()) return;

        _validationCts = new CancellationTokenSource();
        var token = _validationCts.Token;

        Task.Delay(500, token).ContinueWith(
            t => { if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(() => _ = ValidateGoalAsync()); },
            TaskScheduler.Default);
    }

    private async Task ValidateGoalAsync()
    {
        if (!IsStep3Valid()) return;

        _ = int.TryParse(AgeText, out var age);
        var heightCm         = ParseHeightCm();
        var startingWeightKg = ParseWeightToKg(StartingWeightText);
        var targetWeightKg   = ParseWeightToKg(TargetWeightText);

        if (!Gender.HasValue || age <= 0 || !heightCm.HasValue
            || !startingWeightKg.HasValue || !targetWeightKg.HasValue || !TargetPeriod.HasValue)
            return;

        IsValidating = true;
        try
        {
            var result = await _mediator.Send(new ValidateGoalQuery(
                Gender.Value, age, heightCm.Value,
                startingWeightKg.Value, targetWeightKg.Value, TargetPeriod.Value));

            ValidationStatus  = result.Status;
            ValidationMessage = result.Message;
        }
        finally
        {
            IsValidating = false;
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private bool IsStep2Valid()
    {
        if (!Gender.HasValue) return false;
        if (!int.TryParse(AgeText, out var age) || age <= 0) return false;
        if (!StartingWeightValid()) return false;
        return HeightValid();
    }

    private bool IsStep3Valid()
    {
        if (!TargetPeriod.HasValue) return false;
        if (!double.TryParse(TargetWeightText, out var tw) || tw <= 0) return false;
        return true;
    }

    private bool StartingWeightValid() =>
        double.TryParse(StartingWeightText, out var w) && w > 0;

    private bool HeightValid()
    {
        if (IsMetric)
            return double.TryParse(HeightCmText, out var h) && h > 0;

        return int.TryParse(HeightFeetText, out var ft) && ft >= 0
            && double.TryParse(HeightInchesText, out var ins) && ins >= 0
            && (ft > 0 || ins > 0);
    }

    // ── Debounced autosave ────────────────────────────────────────────────────

    public void OnNumericFieldChanged()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(500, token).ContinueWith(
            t => { if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(() => _ = SaveCurrentStateAsync()); },
            TaskScheduler.Default);
    }

    private async Task SaveCurrentStateAsync()
    {
        double? heightCm = ParseHeightCm();
        double? startingWeightKg = ParseWeightToKg(StartingWeightText);
        double? targetWeightKg   = ParseWeightToKg(TargetWeightText);
        _ = int.TryParse(AgeText, out var age);

        await _mediator.Send(new SaveUserProfileCommand(
            UnitSystem:       UnitSystem,
            Gender:           Gender,
            Age:              age > 0 ? age : null,
            HeightCm:         heightCm,
            StartingWeightKg: startingWeightKg,
            TargetWeightKg:   targetWeightKg,
            TargetPeriod:     TargetPeriod
        ));
    }

    private double? ParseHeightCm()
    {
        if (IsMetric)
        {
            return double.TryParse(HeightCmText, out var h) && h > 0 ? h : null;
        }

        bool hasFt  = int.TryParse(HeightFeetText, out var ft);
        bool hasIns = double.TryParse(HeightInchesText, out var ins);
        if (!hasFt && !hasIns) return null;
        return UnitConverter.FeetInchesToCm(hasFt ? ft : 0, hasIns ? ins : 0);
    }

    private double? ParseWeightToKg(string text)
    {
        if (!double.TryParse(text, out var value) || value <= 0) return null;
        return IsMetric ? value : UnitConverter.LbToKg(value);
    }

    // ── Load existing profile on open ─────────────────────────────────────────

    public async Task LoadExistingAsync()
    {
        var dto = await _mediator.Send(new GetUserProfileQuery());
        if (dto is null) return;

        UnitSystem = dto.UnitSystem;

        if (dto.Gender.HasValue)     Gender = dto.Gender;
        if (dto.Age.HasValue)        AgeText = dto.Age.Value.ToString();
        if (dto.TargetPeriod.HasValue) TargetPeriod = dto.TargetPeriod;

        if (dto.StartingWeightKg.HasValue)
            StartingWeightText = IsMetric
                ? dto.StartingWeightKg.Value.ToString("F1")
                : UnitConverter.KgToLb(dto.StartingWeightKg.Value).ToString("F1");

        if (dto.TargetWeightKg.HasValue)
            TargetWeightText = IsMetric
                ? dto.TargetWeightKg.Value.ToString("F1")
                : UnitConverter.KgToLb(dto.TargetWeightKg.Value).ToString("F1");

        if (dto.HeightCm.HasValue)
        {
            if (IsMetric)
            {
                HeightCmText = dto.HeightCm.Value.ToString("F1");
            }
            else
            {
                var (feet, inches) = UnitConverter.CmToFeetAndInches(dto.HeightCm.Value);
                HeightFeetText   = feet.ToString();
                HeightInchesText = inches.ToString("F1");
            }
        }

        // Resume at the furthest incomplete step
        if (!dto.Gender.HasValue || !dto.Age.HasValue || !dto.HeightCm.HasValue || !dto.StartingWeightKg.HasValue)
            CurrentStep = 2;
        else if (!dto.TargetWeightKg.HasValue || !dto.TargetPeriod.HasValue)
            CurrentStep = 3;
    }
}

public class WizardCompletedMessage { }
