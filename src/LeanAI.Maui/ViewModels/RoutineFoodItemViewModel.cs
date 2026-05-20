using CommunityToolkit.Mvvm.ComponentModel;

namespace LeanAI.Maui.ViewModels;

public partial class RoutineFoodItemViewModel : ObservableObject
{
    public Guid? OriginalId { get; init; }

    [ObservableProperty] private string _foodItem  = string.Empty;
    [ObservableProperty] private string _quantity  = string.Empty;
    [ObservableProperty] private double _calories;
    [ObservableProperty] private bool   _isChecked = true;

    public string CaloriesText => Calories > 0 ? $"{Calories:N0} kcal" : "—";

    partial void OnCaloriesChanged(double value) => OnPropertyChanged(nameof(CaloriesText));
}
