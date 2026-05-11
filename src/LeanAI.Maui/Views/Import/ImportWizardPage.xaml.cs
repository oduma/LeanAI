using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Import;

public partial class ImportWizardPage : ContentPage
{
    public ImportWizardPage(ImportWizardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Wire "Done" tap to dismiss the modal
        DoneLabel.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await Navigation.PopModalAsync())
        });
    }
}
