using CommunityToolkit.Mvvm.Messaging;
using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Wizard;

public partial class WizardPage : ContentPage
{
    private readonly WizardViewModel _viewModel;

    public WizardPage(WizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel  = viewModel;
        BindingContext = viewModel;

        WeakReferenceMessenger.Default.Register<WizardCompletedMessage>(this, async (_, _) =>
            await OnWizardCompleted());

        WireDebounce();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadExistingAsync();
    }

    private void WireDebounce()
    {
        AgeEntry.TextChanged           += (_, _) => _viewModel.OnNumericFieldChanged();
        HeightCmEntry.TextChanged      += (_, _) => _viewModel.OnNumericFieldChanged();
        HeightFeetEntry.TextChanged    += (_, _) => _viewModel.OnNumericFieldChanged();
        HeightInchesEntry.TextChanged  += (_, _) => _viewModel.OnNumericFieldChanged();
        StartingWeightEntry.TextChanged += (_, _) => _viewModel.OnNumericFieldChanged();
        TargetWeightEntry.TextChanged  += (_, _) => _viewModel.OnNumericFieldChanged();
    }

    private async Task OnWizardCompleted()
    {
        WeakReferenceMessenger.Default.Unregister<WizardCompletedMessage>(this);
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Unregister<WizardCompletedMessage>(this);
    }
}
