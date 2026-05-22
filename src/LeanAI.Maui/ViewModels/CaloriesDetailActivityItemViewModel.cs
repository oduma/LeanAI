using CommunityToolkit.Mvvm.ComponentModel;
using LeanAI.Application.EnergyTracking.DTOs;

namespace LeanAI.Maui.ViewModels;

public partial class CaloriesDetailActivityItemViewModel(ActivityEnergyLogDto dto) : ObservableObject
{
    public ActivityEnergyLogDto Dto { get; } = dto;

    [ObservableProperty] private bool _isRoutineChecked;
    [ObservableProperty] private bool _isRoutineEnabled = true;
}
