using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.RunReview;

public partial class RunReviewPage : ContentPage
{
    public RunReviewViewModel ViewModel { get; }

    public RunReviewPage(RunReviewViewModel vm)
    {
        InitializeComponent();
        BindingContext = ViewModel = vm;
    }
}
