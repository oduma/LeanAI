using CommunityToolkit.Mvvm.ComponentModel;
using LeanAI.Application.FoodTracking.DTOs;

namespace LeanAI.Maui.ViewModels;

public partial class CaloriesDetailActivityItemViewModel(ActivityCaloryLogDto dto) : ObservableObject
{
    public ActivityCaloryLogDto Dto { get; } = dto;

    [ObservableProperty] private bool _isRoutineChecked;
    [ObservableProperty] private bool _isRoutineEnabled = true;
}
