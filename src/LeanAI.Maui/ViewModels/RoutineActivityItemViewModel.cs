using CommunityToolkit.Mvvm.ComponentModel;

namespace LeanAI.Maui.ViewModels;

public partial class RoutineActivityItemViewModel : ObservableObject
{
    public Guid? OriginalId { get; init; }

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private double _calories;
    [ObservableProperty] private bool   _isChecked = true;

    public string CaloriesText => Calories > 0 ? $"{Calories:N0} kcal" : "—";

    partial void OnCaloriesChanged(double value) => OnPropertyChanged(nameof(CaloriesText));
}
