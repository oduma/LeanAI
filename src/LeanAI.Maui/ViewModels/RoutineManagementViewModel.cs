using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeanAI.Application.ActivityTracking.Commands.EstimateActivityCalories;
using LeanAI.Application.FoodTracking.Commands.RecalculateCalories;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.Routine.Commands.SaveRoutineFromDay;
using LeanAI.Application.Routine.DTOs;
using LeanAI.Application.Routine.Queries.GetRoutineItems;
using MediatR;
using Microsoft.Maui.Controls;

namespace LeanAI.Maui.ViewModels;

public partial class RoutineManagementViewModel(IMediator mediator) : ObservableObject
{
    public ObservableCollection<RoutineFoodItemViewModel>     FoodItems     { get; } = new();
    public ObservableCollection<RoutineActivityItemViewModel> ActivityItems { get; } = new();

    public async Task InitialiseAsync()
    {
        var result = await mediator.Send(new GetRoutineItemsQuery());

        FoodItems.Clear();
        foreach (var item in result.FoodItems)
            FoodItems.Add(new RoutineFoodItemViewModel
            {
                OriginalId = item.Id,
                FoodItem   = item.Description,
                Quantity   = item.Quantity ?? string.Empty,
                Calories   = item.Calories,
                IsChecked  = true
            });

        ActivityItems.Clear();
        foreach (var item in result.ActivityItems)
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

        var foodWithNoCalories = checkedFood.Where(i => i.Calories <= 0).ToList();
        if (foodWithNoCalories.Count > 0)
        {
            var inputs  = foodWithNoCalories.Select(i => new FoodItemInputDto(i.FoodItem, i.Quantity)).ToList();
            var results = await mediator.Send(new RecalculateCaloriesCommand(inputs));
            for (int idx = 0; idx < foodWithNoCalories.Count && idx < results.Count; idx++)
                foodWithNoCalories[idx].Calories = results[idx].Calories;
        }

        var actWithNoCalories = checkedActivities.Where(i => i.Calories <= 0).ToList();
        if (actWithNoCalories.Count > 0)
        {
            var descriptions = actWithNoCalories.Select(i => i.Description).ToList();
            var estimates    = await mediator.Send(new EstimateActivityCaloriesCommand(descriptions));
            for (int idx = 0; idx < actWithNoCalories.Count && idx < estimates.Count; idx++)
                actWithNoCalories[idx].Calories = estimates[idx];
        }

        var foodDtos = checkedFood
            .Select(i => new RoutineFoodItemDto(Guid.Empty, i.FoodItem, string.IsNullOrWhiteSpace(i.Quantity) ? null : i.Quantity, i.Calories))
            .ToList();

        var activityDtos = checkedActivities
            .Select(i => new RoutineActivityItemDto(Guid.Empty, i.Description, i.Calories))
            .ToList();

        await mediator.Send(new SaveRoutineFromDayCommand(foodDtos, activityDtos));
        await Shell.Current.Navigation.PopModalAsync();
    }

    [RelayCommand]
    private Task CancelAsync() => Shell.Current.Navigation.PopModalAsync();
}
