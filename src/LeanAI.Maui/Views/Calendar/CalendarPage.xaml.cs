using LeanAI.Maui.ViewModels;

namespace LeanAI.Maui.Views.Calendar;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _vm;

    public CalendarPage(CalendarViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        CalendarGrid.SizeChanged += OnCalendarGridSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private void OnCalendarGridSizeChanged(object? sender, EventArgs e)
    {
        if (CalendarGrid.Height > 0)
            _vm.UpdateGridHeight(CalendarGrid.Height);
    }
}
