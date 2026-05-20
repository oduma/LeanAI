using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.FoodTracking.Commands.ApplyRoutineForDate;
using LeanAI.Application.FoodTracking.Commands.RemoveUnmodifiedRoutineItems;
using LeanAI.Application.FoodTracking.Commands.SaveRoutineFromDay;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Queries.GetActivityCaloriesForDate;
using LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;
using LeanAI.Application.FoodTracking.Queries.GetRoutineStatusForDate;
using LeanAI.Application.WeightManagement.Queries.GetBmrForDate;
using LeanAI.Maui.Messages;
using LeanAI.Maui.Views.FoodReview;
using LeanAI.Maui.Views.RunReview;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace LeanAI.Maui.ViewModels;

public partial class CaloriesDetailViewModel(IMediator mediator, IServiceProvider serviceProvider)
    : ObservableObject, IRecipient<FoodSavedMessage>, IRecipient<RunSavedMessage>
{
    private DateOnly _date;
    private bool     _isTogglingRoutine;

    [ObservableProperty] private string _bmrText          = "—";
    [ObservableProperty] private bool   _hasBmr;
    [ObservableProperty] private string _foodTotalText     = "—";
    [ObservableProperty] private string _activityTotalText = "—";
    [ObservableProperty] private string _netCaloriesText   = "—";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRoutineCommand))]
    private bool _hasAnyChecked;

    [ObservableProperty] private bool _isRoutineActive;

    public bool AreCheckboxesEnabled => !IsRoutineActive;

    public ObservableCollection<CaloriesDetailFoodItemViewModel>     FoodItemVMs     { get; } = new();
    public ObservableCollection<CaloriesDetailActivityItemViewModel> ActivityItemVMs { get; } = new();

    public async Task InitialiseAsync(DateOnly date)
    {
        _date = date;
        WeakReferenceMessenger.Default.Register<FoodSavedMessage>(this);
        WeakReferenceMessenger.Default.Register<RunSavedMessage>(this);
        await LoadAsync();
    }

    void IRecipient<FoodSavedMessage>.Receive(FoodSavedMessage message)
        => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());

    void IRecipient<RunSavedMessage>.Receive(RunSavedMessage message)
        => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());

    partial void OnIsRoutineActiveChanged(bool value)
    {
        if (_isTogglingRoutine) return;
        _ = HandleRoutineToggleAsync(value);
    }

    private async Task HandleRoutineToggleAsync(bool isActive)
    {
        _isTogglingRoutine = true;
        try
        {
            if (isActive)
                await mediator.Send(new ApplyRoutineForDateCommand(_date));
            else
                await mediator.Send(new RemoveUnmodifiedRoutineItemsForDateCommand(_date));

            await LoadAsync();
            WeakReferenceMessenger.Default.Send(new CaloriesTotalChangedMessage(_date));
        }
        finally
        {
            _isTogglingRoutine = false;
        }
    }

    private async Task LoadAsync()
    {
        var routineActive = await mediator.Send(new GetRoutineStatusForDateQuery(_date));

        var foodLogs = await mediator.Send(new GetFoodLogForDateQuery(_date));
        FoodItemVMs.Clear();
        foreach (var f in foodLogs)
            FoodItemVMs.Add(new CaloriesDetailFoodItemViewModel(f) { IsRoutineEnabled = !routineActive });

        var activityLogs = await mediator.Send(new GetActivityCaloriesForDateQuery(_date));
        ActivityItemVMs.Clear();
        foreach (var a in activityLogs)
            ActivityItemVMs.Add(new CaloriesDetailActivityItemViewModel(a) { IsRoutineEnabled = !routineActive });

        var bmr   = await mediator.Send(new GetBmrForDateQuery(_date));
        HasBmr    = bmr.HasValue;
        BmrText   = bmr.HasValue ? $"−{bmr.Value:N0} kcal" : "—";

        var foodTotal     = FoodItemVMs.Sum(vm => vm.Dto.Calories);
        var activityTotal = ActivityItemVMs.Sum(vm => vm.Dto.Calories);
        var bmrTotal      = bmr ?? 0;
        var net           = foodTotal - activityTotal - bmrTotal;

        FoodTotalText     = foodTotal     > 0 ? $"{foodTotal:N0} kcal"     : "—";
        ActivityTotalText = activityTotal > 0 ? $"{activityTotal:N0} kcal" : "—";
        NetCaloriesText   = !bmr.HasValue && foodTotal == 0 && activityTotal == 0
                            ? "—"
                            : $"{net:N0} kcal";

        _isTogglingRoutine = true;
        IsRoutineActive    = routineActive;
        _isTogglingRoutine = false;
        OnPropertyChanged(nameof(AreCheckboxesEnabled));

        // Subscribe to checkbox changes
        foreach (var vm in FoodItemVMs)
            vm.PropertyChanged += (_, _) => RefreshHasAnyChecked();
        foreach (var vm in ActivityItemVMs)
            vm.PropertyChanged += (_, _) => RefreshHasAnyChecked();

        RefreshHasAnyChecked();
    }

    private void RefreshHasAnyChecked()
        => HasAnyChecked = FoodItemVMs.Any(vm => vm.IsRoutineChecked)
                        || ActivityItemVMs.Any(vm => vm.IsRoutineChecked);

    [RelayCommand(CanExecute = nameof(CanSaveRoutine))]
    private async Task SaveRoutineAsync()
    {
        var items = FoodItemVMs
            .Where(vm => vm.IsRoutineChecked)
            .Select(vm => new RoutineItemDto(Guid.Empty, "food", vm.Dto.FoodItem, vm.Dto.Quantity, vm.Dto.Calories))
            .Concat(ActivityItemVMs
                .Where(vm => vm.IsRoutineChecked)
                .Select(vm => new RoutineItemDto(Guid.Empty, "activity", vm.Dto.Description ?? string.Empty, null, vm.Dto.Calories)))
            .ToList();

        await mediator.Send(new SaveRoutineFromDayCommand(items));

        foreach (var vm in FoodItemVMs)     vm.IsRoutineChecked = false;
        foreach (var vm in ActivityItemVMs) vm.IsRoutineChecked = false;
        RefreshHasAnyChecked();
    }

    private bool CanSaveRoutine() => HasAnyChecked && !IsRoutineActive;

    [RelayCommand]
    private async Task EditFoodAsync()
    {
        var page = serviceProvider.GetRequiredService<FoodReviewPage>();
        await page.ViewModel.InitialiseAsync(_date, isImportMode: false);
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private async Task EditActivitiesAsync()
    {
        var page = serviceProvider.GetRequiredService<RunReviewPage>();
        await page.ViewModel.InitialiseAsync(_date, isImportMode: false);
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private Task CloseAsync()
    {
        WeakReferenceMessenger.Default.Unregister<FoodSavedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<RunSavedMessage>(this);
        return Shell.Current.Navigation.PopModalAsync();
    }
}
