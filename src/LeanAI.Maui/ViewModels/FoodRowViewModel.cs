using CommunityToolkit.Mvvm.ComponentModel;

namespace LeanAI.Maui.ViewModels;

public partial class FoodRowViewModel : ObservableObject
{
    [ObservableProperty] private string _foodItem        = string.Empty;
    [ObservableProperty] private string _quantity        = string.Empty;
    [ObservableProperty] private string _caloriesDisplay = "—";

    public double CaloriesValue { get; set; }

    public void UpdateCalories(double calories)
    {
        CaloriesValue   = calories;
        CaloriesDisplay = $"{calories:N0} kcal";
    }
}
