using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.FoodReview;

public partial class FoodReviewPage : ContentPage
{
    public FoodReviewViewModel ViewModel { get; }

    public FoodReviewPage(FoodReviewViewModel viewModel)
    {
        InitializeComponent();
        ViewModel   = viewModel;
        BindingContext = viewModel;
    }
}
