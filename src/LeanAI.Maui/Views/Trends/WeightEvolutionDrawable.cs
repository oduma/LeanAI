using LeanAI.Application.WeightManagement.DTOs;
using Microsoft.Maui.Graphics;

namespace LeanAI.Maui.Views.Trends;

public class WeightEvolutionDrawable : IDrawable
{
    private const float MarginLeft   = 46f;
    private const float MarginBottom = 28f;
    private const float MarginTop    = 10f;
    private const float MarginRight  = 10f;
    private const float DotRadius    = 5f;
    private const float LineWidth    = 1.5f;
    private const float LabelSize    = 10f;

    private static readonly Color ColorBase   = Color.FromArgb("#222222");
    private static readonly Color ColorNickel = Color.FromArgb("#9A9EAB");
    private static readonly Color ColorCopper = Color.FromArgb("#D28B5C");
    private static readonly Color ColorAmber  = Color.FromArgb("#E8A838");
    private static readonly Color ColorWhite  = Colors.White;
    private static readonly Color ColorGrid   = Color.FromArgb("#333333");

    public IReadOnlyList<WeightPointDto> IdealSeries          { get; set; } = [];
    public IReadOnlyList<WeightPointDto> ActualSeries         { get; set; } = [];
    public IReadOnlyList<WeightPointDto> IdealWeeklyAverages  { get; set; } = [];
    public IReadOnlyList<WeightPointDto> ActualWeeklyAverages { get; set; } = [];
    public DateOnly FirstDate { get; set; }
    public DateOnly LastDate  { get; set; }

    // Zoom mode
    public bool     IsZoomed { get; set; }
    public DateOnly ZoomFrom { get; set; }
    public DateOnly ZoomTo   { get; set; }

    private DateOnly EffectiveFirst => IsZoomed ? ZoomFrom : FirstDate;
    private DateOnly EffectiveLast  => IsZoomed ? ZoomTo   : LastDate;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = ColorBase;
        canvas.FillRectangle(dirtyRect);

        if (IdealSeries.Count == 0) return;

        var plot = new RectF(
            dirtyRect.Left  + MarginLeft,
            dirtyRect.Top   + MarginTop,
            dirtyRect.Width  - MarginLeft   - MarginRight,
            dirtyRect.Height - MarginTop    - MarginBottom);

        // Y-axis range: when zoomed, restrict to in-window data for better granularity
        IEnumerable<double> yIdeal  = IsZoomed
            ? IdealSeries.Where(p => p.Date >= EffectiveFirst && p.Date <= EffectiveLast).Select(p => p.WeightKg)
            : IdealSeries.Select(p => p.WeightKg);
        IEnumerable<double> yActual = IsZoomed
            ? ActualSeries.Where(p => p.Date >= EffectiveFirst && p.Date <= EffectiveLast).Select(p => p.WeightKg)
            : ActualSeries.Select(p => p.WeightKg);

        var allWeights = yIdeal.Concat(yActual).ToList();
        if (allWeights.Count == 0) return;

        var rawMin  = allWeights.Min();
        var rawMax  = allWeights.Max();
        var padding = (rawMax - rawMin) * 0.05;
        var yMin    = rawMin - padding;
        var yMax    = rawMax + padding;

        DrawGrid(canvas, plot, yMin, yMax);
        DrawXLabels(canvas, plot);
        DrawIdealLine(canvas, plot, yMin, yMax);
        DrawActualLine(canvas, plot, yMin, yMax);
        DrawDots(canvas, plot, yMin, yMax, IdealWeeklyAverages, ColorWhite);
        DrawDots(canvas, plot, yMin, yMax, ActualWeeklyAverages, ColorAmber);
    }

    private void DrawGrid(ICanvas canvas, RectF plot, double yMin, double yMax)
    {
        const int ticks = 5;
        canvas.FontSize  = LabelSize;
        canvas.FontColor = ColorNickel;
        canvas.StrokeColor = ColorGrid;
        canvas.StrokeSize  = 0.5f;

        for (var i = 0; i <= ticks; i++)
        {
            var fraction = i / (double)ticks;
            var kg = yMax - fraction * (yMax - yMin);
            var y  = (float)(plot.Top + fraction * plot.Height);

            canvas.DrawLine(plot.Left, y, plot.Right, y);
            canvas.DrawString($"{kg:F1}", plot.Left - MarginLeft, y - LabelSize / 2, MarginLeft - 4, LabelSize, HorizontalAlignment.Right, VerticalAlignment.Center);
        }
    }

    private void DrawXLabels(ICanvas canvas, RectF plot)
    {
        canvas.FontSize  = LabelSize;
        canvas.FontColor = ColorNickel;

        var current = new DateOnly(EffectiveFirst.Year, EffectiveFirst.Month, 1).AddMonths(1);
        while (current <= EffectiveLast)
        {
            var x = DateToX(current, plot);
            if (x >= plot.Left && x <= plot.Right)
            {
                canvas.StrokeColor = ColorGrid;
                canvas.StrokeSize  = 0.5f;
                canvas.DrawLine(x, plot.Top, x, plot.Bottom);
                canvas.DrawString(
                    current.ToString("d MMM"),
                    x - 20, plot.Bottom + 2, 40, MarginBottom - 2,
                    HorizontalAlignment.Center, VerticalAlignment.Top);
            }
            current = current.AddMonths(1);
        }
    }

    private void DrawIdealLine(ICanvas canvas, RectF plot, double yMin, double yMax)
    {
        canvas.StrokeColor = ColorNickel;
        canvas.StrokeSize  = LineWidth;

        for (var i = 1; i < IdealSeries.Count; i++)
        {
            var prev = IdealSeries[i - 1];
            var curr = IdealSeries[i];
            canvas.DrawLine(
                DateToX(prev.Date, plot), WeightToY(prev.WeightKg, plot, yMin, yMax),
                DateToX(curr.Date, plot), WeightToY(curr.WeightKg, plot, yMin, yMax));
        }
    }

    private void DrawActualLine(ICanvas canvas, RectF plot, double yMin, double yMax)
    {
        canvas.StrokeColor = ColorCopper;
        canvas.StrokeSize  = LineWidth;

        for (var i = 1; i < ActualSeries.Count; i++)
        {
            var prev = ActualSeries[i - 1];
            var curr = ActualSeries[i];
            // Only draw segment between consecutive calendar dates
            if (curr.Date != prev.Date.AddDays(1)) continue;

            canvas.DrawLine(
                DateToX(prev.Date, plot), WeightToY(prev.WeightKg, plot, yMin, yMax),
                DateToX(curr.Date, plot), WeightToY(curr.WeightKg, plot, yMin, yMax));
        }
    }

    private void DrawDots(ICanvas canvas, RectF plot, double yMin, double yMax,
        IReadOnlyList<WeightPointDto> series, Color color)
    {
        canvas.FillColor = color;
        foreach (var p in series)
        {
            var cx = DateToX(p.Date, plot);
            var cy = WeightToY(p.WeightKg, plot, yMin, yMax);
            canvas.FillCircle(cx, cy, DotRadius);
        }
    }

    private float DateToX(DateOnly date, RectF plot)
    {
        var totalDays = EffectiveLast.DayNumber - EffectiveFirst.DayNumber;
        if (totalDays == 0) return plot.Left;
        var fraction = (date.DayNumber - EffectiveFirst.DayNumber) / (float)totalDays;
        return plot.Left + fraction * plot.Width;
    }

    private static float WeightToY(double kg, RectF plot, double yMin, double yMax)
    {
        var range = yMax - yMin;
        if (range == 0) return plot.Top + plot.Height / 2f;
        var fraction = (yMax - kg) / range;
        return (float)(plot.Top + fraction * plot.Height);
    }
}
