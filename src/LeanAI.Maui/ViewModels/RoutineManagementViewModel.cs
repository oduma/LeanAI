using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeanAI.Application.ActivityTracking.Commands.EstimateActivityCalories;
using LeanAI.Application.FoodTracking.Commands.SaveRoutineFromDay;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Commands.RecalculateCalories;
using LeanAI.Application.FoodTracking.Queries.GetRoutineItems;
using MediatR;
using Microsoft.Maui.Controls;

namespace LeanAI.Maui.ViewModels;

public partial class RoutineManagementViewModel(IMediator mediator) : ObservableObject
{
    public ObservableCollection<RoutineFoodItemViewModel>     FoodItems     { get; } = new();
    public ObservableCollection<RoutineActivityItemViewModel> ActivityItems { get; } = new();

    public async Task InitialiseAsync()
    {
        var items = await mediator.Send(new GetRoutineItemsQuery());

        FoodItems.Clear();
        foreach (var item in items.Where(i => i.SourceType == "food"))
            FoodItems.Add(new RoutineFoodItemViewModel
            {
                OriginalId  = item.Id,
                FoodItem    = item.Description,
                Quantity    = item.Quantity ?? string.Empty,
                Calories    = item.Calories,
                IsChecked   = true
            });

        ActivityItems.Clear();
        foreach (var item in items.Where(i => i.SourceType == "activity"))
            ActivityItems.Add(new RoutineActivityItemViewModel
            {
                OriginalId  = item.Id,
                Description = item.Description,
                Calories    = item.Calories,
                IsChecked   = true
            });
    }

    [RelayCommand]
    private void AddFoodItem()
        => FoodItems.Add(new RoutineFoodItemViewModel { IsChecked = true });

    [RelayCommand]
    private void AddActivity()
        => ActivityItems.Add(new RoutineActivityItemViewModel { IsChecked = true });

    [RelayCommand]
    private async Task SaveAsync()
    {
        var checkedFood       = FoodItems.Where(i => i.IsChecked).ToList();
        var checkedActivities = ActivityItems.Where(i => i.IsChecked).ToList();

        // Evaluate calories for food items that have none
        var foodWithNoCalories = checkedFood.Where(i => i.Calories <= 0).ToList();
        if (foodWithNoCalories.Count > 0)
        {
            var inputs  = foodWithNoCalories.Select(i => new FoodItemInputDto(i.FoodItem, i.Quantity)).ToList();
            var results = await mediator.Send(new RecalculateCaloriesCommand(inputs));
            for (int idx = 0; idx < foodWithNoCalories.Count && idx < results.Count; idx++)
                foodWithNoCalories[idx].Calories = results[idx].Calories;
        }

        // Evaluate calories for activity items that have none
        var actWithNoCalories = checkedActivities.Where(i => i.Calories <= 0).ToList();
        if (actWithNoCalories.Count > 0)
        {
            var descriptions = actWithNoCalories.Select(i => i.Description).ToList();
            var estimates    = await mediator.Send(new EstimateActivityCaloriesCommand(descriptions));
            for (int idx = 0; idx < actWithNoCalories.Count && idx < estimates.Count; idx++)
                actWithNoCalories[idx].Calories = estimates[idx];
        }

        var dtos = checkedFood
            .Select(i => new RoutineItemDto(Guid.Empty, "food", i.FoodItem, string.IsNullOrWhiteSpace(i.Quantity) ? null : i.Quantity, i.Calories))
            .Concat(checkedActivities
                .Select(i => new RoutineItemDto(Guid.Empty, "activity", i.Description, null, i.Calories)))
            .ToList();

        await mediator.Send(new SaveRoutineFromDayCommand(dtos));
        await Shell.Current.Navigation.PopModalAsync();
    }

    [RelayCommand]
    private Task CancelAsync() => Shell.Current.Navigation.PopModalAsync();
}
