using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Trends;

public partial class TrendsPage : ContentPage
{
    private TrendsViewModel ViewModel => (TrendsViewModel)BindingContext;

    public TrendsPage(TrendsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
        EvolutionView.Invalidate();
    }
}
