using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.ActivityTracking.Commands.EstimateActivityCalories;
using LeanAI.Application.ActivityTracking.Commands.SaveRunActivities;
using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.EnergyTracking.Queries.GetActivityCaloriesForDate;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Infrastructure.ActivityTracking.Services;
using LeanAI.Maui.Messages;
using MediatR;
using Microsoft.Maui.Controls;

namespace LeanAI.Maui.ViewModels;

public partial class RunReviewViewModel(IMediator mediator, RunImportStateService runImportState)
    : ObservableObject
{
    [ObservableProperty] private DateTime _selectedDate;
    [ObservableProperty] private DateTime _minDate = new DateTime(2000, 1, 1);
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;
    [ObservableProperty] private bool     _isImportMode;
    [ObservableProperty] private bool     _isBusy;
    [ObservableProperty] private string   _totalCaloriesText = "—";

    public ObservableCollection<RunRowViewModel> Items { get; } = new();

    public async Task InitialiseAsync(DateOnly date, bool isImportMode)
    {
        Items.CollectionChanged -= OnItemsCollectionChanged;
        Items.CollectionChanged += OnItemsCollectionChanged;

        IsImportMode = isImportMode;
        MaxDate      = DateTime.Today;

        var profile = await mediator.Send(new GetUserProfileQuery());
        MinDate     = profile?.GoalStartDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today.AddYears(-1);

        _selectedDate = date.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(SelectedDate));

        Items.Clear();

        if (isImportMode)
        {
            var pending = runImportState.Take();
            if (pending is not null)
                foreach (var row in pending)
                    Items.Add(ToRowViewModel(row));

            if (Items.Any(r => r.CaloriesValue == 0))
                await ReEvaluateAsync();
        }
        else
        {
            await LoadActivityCaloriesAsync(date);
        }

        RefreshTotal();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (IsImportMode) return;
        _ = LoadActivityCaloriesAsync(DateOnly.FromDateTime(value));
    }

    private async Task LoadActivityCaloriesAsync(DateOnly date)
    {
        Items.Clear();
        var entries = await mediator.Send(new GetActivityCaloriesForDateQuery(date));
        foreach (var entry in entries)
        {
            var row = new RunRowViewModel { IsRunRow = false };
            row.ActivityText = entry.Description ?? string.Empty;
            row.UpdateCalories(entry.Calories);
            Items.Add(row);
        }
        RefreshTotal();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshTotal();

    private void RefreshTotal()
    {
        var total = Items.Sum(r => r.CaloriesValue);
        TotalCaloriesText = total > 0 ? $"{total:N0} kcal" : "—";
    }

    [RelayCommand]
    private void AddItem()
        => Items.Add(new RunRowViewModel { IsRunRow = false });

    [RelayCommand]
    private void DeleteItem(RunRowViewModel row)
        => Items.Remove(row);

    [RelayCommand]
    private async Task ReEvaluateAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var descriptions = Items.Select(r => r.ActivityText).ToList();
            if (descriptions.Count == 0) return;

            var estimates = await mediator.Send(new EstimateActivityCaloriesCommand(descriptions));

            for (var i = 0; i < Math.Min(Items.Count, estimates.Count); i++)
                Items[i].UpdateCalories(estimates[i]);

            RefreshTotal();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var targetDate = DateOnly.FromDateTime(SelectedDate);
            var rows = Items
                .Select(r => new RunActivityRowDto(
                    r.ActivityText,
                    r.CaloriesValue,
                    r.IsRunRow,
                    r.Metrics))
                .ToList();

            await mediator.Send(new SaveRunActivitiesCommand(targetDate, rows, IsImportMode));
            await Shell.Current.Navigation.PopModalAsync();
            WeakReferenceMessenger.Default.Send(new RunSavedMessage(targetDate));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAllAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete All",
            "Remove all activity entries for this day?",
            "Delete", "Cancel");

        if (!confirmed) return;

        var targetDate = DateOnly.FromDateTime(SelectedDate);
        await mediator.Send(new SaveRunActivitiesCommand(targetDate, [], IsImportMode: false));
        Items.Clear();
        await Shell.Current.Navigation.PopModalAsync();
    }

    [RelayCommand]
    private Task CancelAsync()
        => Shell.Current.Navigation.PopModalAsync();

    private static RunRowViewModel ToRowViewModel(RunActivityRowDto dto)
    {
        var row = new RunRowViewModel
        {
            IsRunRow = dto.IsRunRow,
            Metrics  = dto.Metrics
        };
        row.ActivityText = dto.ActivityText;
        row.UpdateCalories(dto.Calories);
        return row;
    }
}
