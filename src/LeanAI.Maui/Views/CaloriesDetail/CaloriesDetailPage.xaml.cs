using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.CaloriesDetail;

public partial class CaloriesDetailPage : ContentPage
{
    public CaloriesDetailViewModel ViewModel { get; }

    public CaloriesDetailPage(CaloriesDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = ViewModel = vm;
    }
}
