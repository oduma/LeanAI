using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Queries.GetActivityCaloriesForDate;
using LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;
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

    [ObservableProperty] private string _foodTotalText     = "—";
    [ObservableProperty] private string _activityTotalText = "—";
    [ObservableProperty] private string _netCaloriesText   = "—";

    public ObservableCollection<FoodLogEntryDto>      FoodItems     { get; } = new();
    public ObservableCollection<ActivityCaloryLogDto> ActivityItems { get; } = new();

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

    private async Task LoadAsync()
    {
        var foodLogs = await mediator.Send(new GetFoodLogForDateQuery(_date));
        FoodItems.Clear();
        foreach (var f in foodLogs)
            FoodItems.Add(f);

        var activityLogs = await mediator.Send(new GetActivityCaloriesForDateQuery(_date));
        ActivityItems.Clear();
        foreach (var a in activityLogs)
            ActivityItems.Add(a);

        var foodTotal     = FoodItems.Sum(f => f.Calories);
        var activityTotal = ActivityItems.Sum(a => a.Calories);
        var net           = foodTotal - activityTotal;

        FoodTotalText     = foodTotal     > 0  ? $"{foodTotal:N0} kcal"     : "—";
        ActivityTotalText = activityTotal > 0  ? $"{activityTotal:N0} kcal" : "—";
        NetCaloriesText   = net           != 0 ? $"{net:N0} kcal"           : "—";
    }

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
