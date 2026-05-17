using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.FoodTracking.Commands.DeleteFoodLog;
using LeanAI.Application.FoodTracking.Commands.RecalculateCalories;
using LeanAI.Application.FoodTracking.Commands.SaveFoodLog;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;
using LeanAI.Application.WeightManagement.Queries.GetUserProfile;
using LeanAI.Infrastructure.FoodTracking.Services;
using LeanAI.Maui.Messages;
using MediatR;
using Microsoft.Maui.Controls;

namespace LeanAI.Maui.ViewModels;

public partial class FoodReviewViewModel(IMediator mediator, FoodImportStateService foodImportState)
    : ObservableObject
{
    [ObservableProperty] private DateTime _selectedDate;
    [ObservableProperty] private DateTime _minDate = new DateTime(2000, 1, 1);
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;
    [ObservableProperty] private bool     _isImportMode;
    [ObservableProperty] private bool     _isBusy;
    [ObservableProperty] private string   _totalCaloriesText = "—";

    public ObservableCollection<FoodRowViewModel> Items { get; } = new();

    public async Task InitialiseAsync(DateOnly date, bool isImportMode)
    {
        Items.CollectionChanged -= OnItemsCollectionChanged;
        Items.CollectionChanged += OnItemsCollectionChanged;

        IsImportMode = isImportMode;
        MaxDate      = DateTime.Today;

        var profile  = await mediator.Send(new GetUserProfileQuery());
        MinDate      = profile?.GoalStartDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today.AddYears(-1);

        // Set date without triggering reload (items are about to be loaded below)
        _selectedDate = date.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(SelectedDate));

        Items.Clear();

        if (isImportMode)
        {
            var pending = foodImportState.Take();
            if (pending is not null)
                foreach (var item in pending)
                    Items.Add(ToRow(item));
        }
        else
        {
            await LoadFoodForDateAsync(date);
        }

        RefreshTotal();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (IsImportMode) return;
        _ = LoadFoodForDateAsync(DateOnly.FromDateTime(value));
    }

    private async Task LoadFoodForDateAsync(DateOnly date)
    {
        Items.Clear();
        var entries = await mediator.Send(new GetFoodLogForDateQuery(date));
        foreach (var entry in entries)
        {
            var row = new FoodRowViewModel { FoodItem = entry.FoodItem, Quantity = entry.Quantity };
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
        => Items.Add(new FoodRowViewModel());

    [RelayCommand]
    private void DeleteItem(FoodRowViewModel row)
        => Items.Remove(row);

    [RelayCommand]
    private async Task ReEvaluateAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var inputs = Items
                .Select(r => new FoodItemInputDto(r.FoodItem, r.Quantity))
                .ToList();

            var updated = await mediator.Send(new RecalculateCaloriesCommand(inputs));

            for (var i = 0; i < Math.Min(Items.Count, updated.Count); i++)
                Items[i].UpdateCalories(updated[i].Calories);

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
            var dtos = Items
                .Select(r => new FoodItemDto(r.FoodItem, r.Quantity, r.CaloriesValue))
                .ToList();

            await mediator.Send(new SaveFoodLogCommand(targetDate, dtos));
            await Shell.Current.Navigation.PopModalAsync();
            WeakReferenceMessenger.Default.Send(new FoodSavedMessage(targetDate));
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
            "Remove all food entries for this day?",
            "Delete", "Cancel");

        if (!confirmed) return;

        var targetDate = DateOnly.FromDateTime(SelectedDate);
        await mediator.Send(new DeleteFoodLogForDateCommand(targetDate));
        Items.Clear();
        await Shell.Current.Navigation.PopModalAsync();
    }

    [RelayCommand]
    private Task CancelAsync()
        => Shell.Current.Navigation.PopModalAsync();

    private static FoodRowViewModel ToRow(FoodItemDto dto)
    {
        var row = new FoodRowViewModel
        {
            FoodItem = dto.FoodItem,
            Quantity = dto.Quantity
        };
        row.UpdateCalories(dto.Calories);
        return row;
    }
}
