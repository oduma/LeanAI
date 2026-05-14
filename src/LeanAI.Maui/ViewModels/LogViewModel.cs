using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeanAI.Application.WeightManagement.Commands.UpsertDailyLog;
using LeanAI.Application.WeightManagement.Queries.GetLogContext;
using LeanAI.Domain.WeightManagement.Enums;
using MediatR;

namespace LeanAI.Maui.ViewModels;

public partial class LogViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    private double?    _yesterdayWeightKg;
    private double?    _todayIdealWeightKg;
    private double?    _weekFirstWeightKg;
    private int        _weekDaysLogged;
    private double?    _idealWeeklyLossKg;
    private double?    _currentWeekAverageKg;
    private double?    _lastWeekAverageKg;
    private UnitSystem _unitSystem           = UnitSystem.Metric;
    private bool       _todayWasAlreadySaved;
    private bool       _isLoading;
    private bool       _dateWasExplicitlySet;   // set by LoadForDateAsync (modal path)
    private CancellationTokenSource? _saveCts;

    [ObservableProperty] private DateOnly _entryDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string   _weightDisplayText  = string.Empty;
    [ObservableProperty] private string?  _notes;
    [ObservableProperty] private bool     _isAutosaving;
    [ObservableProperty] private string   _yesterdayDeltaText = "—";
    [ObservableProperty] private bool     _isDeltaGain;
    [ObservableProperty] private bool     _isAboveIdeal;
    [ObservableProperty] private string   _aboveIdealDeltaText = string.Empty;
    [ObservableProperty] private string   _currentWeeklyLossText    = "—";
    [ObservableProperty] private string   _idealWeeklyLossText      = "—";
    [ObservableProperty] private string   _unitLabel                = "kg";
    [ObservableProperty] private string   _entryDateLabel           = string.Empty;
    [ObservableProperty] private string   _weekStartDateLabel       = string.Empty;
    [ObservableProperty] private string   _currentWeeklyAverageText = "—";
    [ObservableProperty] private bool     _isWeeklyAverageTrending;

    public LogViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    partial void OnWeightDisplayTextChanged(string value)
    {
        if (_isLoading) return;
        RecalculateIndicators();
        TriggerSave();
    }

    partial void OnNotesChanged(string? value)
    {
        if (_isLoading) return;
        TriggerSave();
    }

    /// <summary>Called by CalendarPage when pushing this page modally for a specific date.</summary>
    public async Task LoadForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        _dateWasExplicitlySet = true;
        await LoadCoreAsync(date, ct);
    }

    [RelayCommand]
    private async Task LoadLogAsync()
    {
        // Guard: if the date was already set via LoadForDateAsync (modal path), skip the tab-flow re-load.
        if (_dateWasExplicitlySet) return;
        await LoadCoreAsync(DateOnly.FromDateTime(DateTime.Today));
    }

    private async Task LoadCoreAsync(DateOnly date, CancellationToken ct = default)
    {
        EntryDate      = date;
        EntryDateLabel = date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        var ctx = await _mediator.Send(new GetLogContextQuery(EntryDate), ct);

        _yesterdayWeightKg    = ctx.YesterdayWeightKg;
        _todayIdealWeightKg   = ctx.TodayIdealWeightKg;
        _weekFirstWeightKg    = ctx.WeekFirstWeightKg;
        _weekDaysLogged       = ctx.WeekDaysLogged;
        _idealWeeklyLossKg    = ctx.IdealWeeklyLossKg;
        _currentWeekAverageKg = ctx.CurrentWeekAverageWeightKg;
        _lastWeekAverageKg    = ctx.LastWeekAverageWeightKg;
        _unitSystem           = ctx.UnitSystem;
        _todayWasAlreadySaved = ctx.TodayWeightKg.HasValue;

        UnitLabel         = _unitSystem == UnitSystem.Metric ? "kg" : "lb";
        WeekStartDateLabel = ctx.WeekStartDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        _isLoading = true;
        WeightDisplayText = ctx.TodayWeightKg.HasValue
            ? FormatValue(ToDisplay(ctx.TodayWeightKg.Value))
            : string.Empty;
        Notes      = ctx.TodayNotes;
        _isLoading = false;

        RecalculateIndicators();
    }

    private void RecalculateIndicators()
    {
        var currentKg = ParseToKg(WeightDisplayText);

        if (currentKg is null)
        {
            YesterdayDeltaText       = "—";
            IsDeltaGain              = false;
            IsAboveIdeal             = false;
            AboveIdealDeltaText      = string.Empty;
            CurrentWeeklyLossText    = "—";
            IdealWeeklyLossText      = FormatWeightOrDash(_idealWeeklyLossKg);
            CurrentWeeklyAverageText = _currentWeekAverageKg.HasValue
                ? $"{FormatValue(ToDisplay(_currentWeekAverageKg.Value))} {UnitLabel}"
                : "—";
            IsWeeklyAverageTrending  = _lastWeekAverageKg.HasValue && _currentWeekAverageKg.HasValue
                && _currentWeekAverageKg.Value < _lastWeekAverageKg.Value;
            return;
        }

        // Yesterday delta
        if (_yesterdayWeightKg.HasValue)
        {
            var deltaKg      = currentKg.Value - _yesterdayWeightKg.Value;
            var displayDelta = ToDisplay(Math.Abs(deltaKg));
            IsDeltaGain        = deltaKg > 0;
            YesterdayDeltaText = deltaKg > 0
                ? $"+{FormatValue(displayDelta)} {UnitLabel}"
                : $"−{FormatValue(displayDelta)} {UnitLabel}";
        }
        else
        {
            YesterdayDeltaText = "—";
            IsDeltaGain        = false;
        }

        // Ideal comparison
        if (_todayIdealWeightKg.HasValue)
        {
            IsAboveIdeal = currentKg.Value > _todayIdealWeightKg.Value;
            if (IsAboveIdeal)
            {
                var diff = ToDisplay(currentKg.Value - _todayIdealWeightKg.Value);
                AboveIdealDeltaText = $"+{FormatValue(diff)} {UnitLabel}";
            }
            else
            {
                AboveIdealDeltaText = string.Empty;
            }
        }
        else
        {
            IsAboveIdeal        = false;
            AboveIdealDeltaText = string.Empty;
        }

        // Ideal weekly loss
        IdealWeeklyLossText = FormatWeightOrDash(_idealWeeklyLossKg);

        // Projected weekly loss
        var effectiveWeekFirst = _weekFirstWeightKg;
        var effectiveDays      = _weekDaysLogged;
        if (!_todayWasAlreadySaved)
        {
            effectiveWeekFirst ??= currentKg.Value;
            effectiveDays++;
        }

        if (effectiveDays >= 2 && effectiveWeekFirst.HasValue)
        {
            var weekLoss  = effectiveWeekFirst.Value - currentKg.Value;
            var projected = ToDisplay(Math.Abs(weekLoss / effectiveDays * 7.0));
            CurrentWeeklyLossText = $"{FormatValue(projected)} {UnitLabel}";
        }
        else
        {
            CurrentWeeklyLossText = "—";
        }

        // Current average weekly weight
        if (_currentWeekAverageKg.HasValue)
        {
            CurrentWeeklyAverageText = $"{FormatValue(ToDisplay(_currentWeekAverageKg.Value))} {UnitLabel}";
            IsWeeklyAverageTrending  = _lastWeekAverageKg.HasValue
                && _currentWeekAverageKg.Value < _lastWeekAverageKg.Value;
        }
        else
        {
            CurrentWeeklyAverageText = "—";
            IsWeeklyAverageTrending  = false;
        }
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
            var weightKg = ParseToKg(WeightDisplayText);
            if (weightKg is null) return;

            IsAutosaving = true;
            await _mediator.Send(new UpsertDailyLogCommand(EntryDate, weightKg.Value, Notes), ct);

            if (!_todayWasAlreadySaved)
            {
                _todayWasAlreadySaved = true;
                _weekDaysLogged++;
                _weekFirstWeightKg ??= weightKg.Value;
            }

            IsAutosaving = false;
        }
        catch (OperationCanceledException)
        {
            // Expected — new debounce cycle superseded this one
        }
        catch
        {
            IsAutosaving = false;
        }
    }

    private double? ParseToKg(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) return null;
        return _unitSystem == UnitSystem.Imperial ? value / 2.20462 : value;
    }

    private double ToDisplay(double kg) =>
        _unitSystem == UnitSystem.Imperial ? kg * 2.20462 : kg;

    private static string FormatValue(double v) =>
        v.ToString("F1", CultureInfo.InvariantCulture);

    private string FormatWeightOrDash(double? kg)
    {
        if (!kg.HasValue) return "—";
        return $"{FormatValue(ToDisplay(kg.Value))} {UnitLabel}";
    }
}
