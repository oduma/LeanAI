using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
