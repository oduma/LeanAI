using CommunityToolkit.Mvvm.ComponentModel;
using LeanAI.Application.ActivityTracking.DTOs;

namespace LeanAI.Maui.ViewModels;

public partial class RunRowViewModel : ObservableObject
{
    [ObservableProperty] private string _activityText    = string.Empty;
    [ObservableProperty] private string _caloriesDisplay = "—";

    public double                            CaloriesValue { get; set; }
    public bool                              IsRunRow      { get; set; }
    public IReadOnlyList<ActivityMetricDto>? Metrics       { get; set; }

    public void UpdateCalories(double calories)
    {
        CaloriesValue   = calories;
        CaloriesDisplay = calories > 0 ? $"{calories:N0} kcal" : "—";
    }
}
