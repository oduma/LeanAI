using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeanAI.Application.WeightManagement.Queries.GetCalendarMonth;
using MediatR;

namespace LeanAI.Maui.ViewModels;

public partial class CalendarViewModel(IMediator mediator, IServiceProvider serviceProvider) : ObservableObject
{
    [ObservableProperty] private int    _currentYear;
    [ObservableProperty] private int    _currentMonth;
    [ObservableProperty] private string _monthLabel  = string.Empty;
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasData;
    [ObservableProperty] private bool   _canGoPrev = true;
    [ObservableProperty] private bool   _canGoNext = true;
    [ObservableProperty] private double _cellHeight = 70.0;

    private int    _weekRowCount = 5;
    private double _gridHeight   = 0;

    public ObservableCollection<CalendarDayViewModel> Days          { get; } = new();
    public ObservableCollection<string>               ColumnHeaders { get; } = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (CurrentYear == 0)
        {
            CurrentYear  = DateTime.Today.Year;
            CurrentMonth = DateTime.Today.Month;
        }
        await RefreshAsync(ct);
    }

    public void UpdateGridHeight(double height)
    {
        _gridHeight = height;
        RecalculateCellHeight();
    }

    private void RecalculateCellHeight()
    {
        if (_weekRowCount > 0 && _gridHeight > 0)
            CellHeight = Math.Max(44, _gridHeight / _weekRowCount);
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        var prev = new DateTime(CurrentYear, CurrentMonth, 1).AddMonths(-1);
        CurrentYear  = prev.Year;
        CurrentMonth = prev.Month;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        var next = new DateTime(CurrentYear, CurrentMonth, 1).AddMonths(1);
        CurrentYear  = next.Year;
        CurrentMonth = next.Month;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DayTappedAsync(CalendarDayViewModel day)
    {
        if (!day.IsClickable) return;

        var logVm = serviceProvider.GetRequiredService<LogViewModel>();
        await logVm.LoadForDateAsync(day.Date);

        var page = serviceProvider.GetRequiredService<Views.Log.LogPage>();
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    private async Task RefreshAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            var dto = await mediator.Send(new GetCalendarMonthQuery(CurrentYear, CurrentMonth), ct);
            HasData = dto is not null;

            Days.Clear();
            ColumnHeaders.Clear();

            if (dto is null) return;

            MonthLabel = new DateTime(CurrentYear, CurrentMonth, 1)
                .ToString("MMMM yyyy", CultureInfo.InvariantCulture);

            BuildColumnHeaders(dto.FirstDayOfWeek);

            foreach (var day in dto.Days)
                Days.Add(CalendarDayViewModel.FromDto(day));

            _weekRowCount = Math.Max(1, Days.Count / 7);
            RecalculateCellHeight();
        }
        finally { IsBusy = false; }
    }

    private void BuildColumnHeaders(DayOfWeek firstDay)
    {
        // Short day names starting from firstDay
        var names = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames;
        for (var i = 0; i < 7; i++)
        {
            var dow = (DayOfWeek)(((int)firstDay + i) % 7);
            ColumnHeaders.Add(names[(int)dow].ToUpper()[..2]);
        }
    }
}
