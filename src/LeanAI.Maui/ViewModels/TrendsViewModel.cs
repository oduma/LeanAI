using CommunityToolkit.Mvvm.ComponentModel;
using LeanAI.Application.WeightManagement.Queries.GetTrendsData;
using LeanAI.Maui.Views.Trends;
using MediatR;
using Microcharts;
using SkiaSharp;

namespace LeanAI.Maui.ViewModels;

public partial class TrendsViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private Chart? _weeklyDeltaChart;

    public WeightEvolutionDrawable EvolutionDrawable { get; } = new();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            var dto = await mediator.Send(new GetTrendsDataQuery(), ct);
            HasData = dto is not null;
            if (dto is null) return;

            EvolutionDrawable.IdealSeries          = dto.IdealSeries;
            EvolutionDrawable.ActualSeries         = dto.ActualSeries;
            EvolutionDrawable.IdealWeeklyAverages  = dto.IdealWeeklyAverages;
            EvolutionDrawable.ActualWeeklyAverages = dto.ActualWeeklyAverages;
            EvolutionDrawable.FirstDate            = dto.FirstDate;
            EvolutionDrawable.LastDate             = dto.LastDate;

            WeeklyDeltaChart = BuildWeeklyDeltaChart(dto);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static Chart BuildWeeklyDeltaChart(
        LeanAI.Application.WeightManagement.DTOs.TrendsDataDto dto)
    {
        var entries = dto.WeeklyDeltas
            .Select(d => new ChartEntry((float)d.DeltaKg)
            {
                Color     = d.DeltaKg >= 0
                    ? SKColor.Parse("#D28B5C")   // copper — lost weight
                    : SKColor.Parse("#9A9EAB"),  // nickel — gained weight
                Label     = d.WeekStart.ToString("d MMM"),
                TextColor = SKColor.Parse("#9A9EAB"),
            })
            .ToArray();

        return new BarChart
        {
            Entries         = entries,
            BackgroundColor = SKColor.Parse("#222222"),
            LabelTextSize   = 24f,
            Margin          = 10,
        };
    }
}
