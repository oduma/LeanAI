using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Routine;

public partial class RoutineManagementPage : ContentPage
{
    public RoutineManagementViewModel ViewModel { get; }

    public RoutineManagementPage(RoutineManagementViewModel viewModel)
    {
        InitializeComponent();
        ViewModel       = viewModel;
        BindingContext  = viewModel;
    }

    public Task InitialiseAsync() => ViewModel.InitialiseAsync();
}
