using CommunityToolkit.Mvvm.ComponentModel;
using LeanAI.Application.WeightManagement.DTOs;

namespace LeanAI.Maui.ViewModels;

public partial class CalendarDayViewModel : ObservableObject
{
    public DateOnly         Date              { get; init; }
    public int              DayNumber         { get; init; }
    public bool             IsInDisplayedMonth { get; init; }
    public bool             IsClickable       { get; init; }
    public CalendarDayState State             { get; init; }

    // Visual colours resolved at construction time from DTO state.
    public Color  HaloStrokeColor  { get; init; } = Colors.Transparent;
    public bool   HasHalo          { get; init; }
    public Color  DayNumberColor   { get; init; } = Color.FromArgb("#9A9EAB");
    public double Opacity          { get; init; } = 1.0;

    public double? WeightKg { get; init; }

    public static CalendarDayViewModel FromDto(CalendarDayDto dto)
    {
        var copper = Color.FromArgb("#D28B5C");
        var nickel = Color.FromArgb("#9A9EAB");

        var opacity = dto.State is CalendarDayState.OutOfRange or CalendarDayState.FutureInRange
            ? 0.35
            : (dto.IsInDisplayedMonth ? 1.0 : 0.6);

        var hasHalo = dto.State == CalendarDayState.PastHasRecord && dto.Halo != CalendarHaloColor.None;
        var haloColor = dto.Halo == CalendarHaloColor.Copper ? copper : nickel;

        Color dayNumberColor = dto.State switch
        {
            CalendarDayState.PastHasRecord when dto.TextTrend == CalendarTrendColor.Copper => copper,
            CalendarDayState.PastHasRecord when dto.TextTrend == CalendarTrendColor.Nickel => nickel,
            CalendarDayState.PastHasRecord => Color.FromArgb("#F0F2F5"),  // TextTrend.None
            CalendarDayState.PastNoRecord  => nickel,
            _                              => nickel,
        };

        var isClickable = dto.State is CalendarDayState.PastNoRecord or CalendarDayState.PastHasRecord;

        return new CalendarDayViewModel
        {
            Date               = dto.Date,
            DayNumber          = dto.Date.Day,
            IsInDisplayedMonth = dto.IsInDisplayedMonth,
            IsClickable        = isClickable,
            State              = dto.State,
            HasHalo            = hasHalo,
            HaloStrokeColor    = haloColor,
            DayNumberColor     = dayNumberColor,
            Opacity            = opacity,
            WeightKg           = dto.WeightKg,
        };
    }
}
