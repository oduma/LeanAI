using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
            vm.LoadAiSettingsCommand.Execute(null);
    }
}
