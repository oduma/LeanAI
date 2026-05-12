# Phase 5: The Trends Screen

**Status:** ✅ COMPLETE — shipped 2026-05-12  
**Branch:** main  
**Tests:** 95 / 95 passing

---

## Scope

Phase 5 delivers the Trends tab — already wired in `AppShell.xaml` as a placeholder — with two stacked charts:

1. **Weight Evolution chart** (top, ~320px): full goal period showing ideal and actual weight lines with weekly average dots  
2. **Weekly Loss/Gain chart** (bottom, ~160px): bar chart of week-over-week average weight change

Calendar Achievement Matrix is **out of scope** for Phase 5 (deferred to a future phase).

Phase 5 also fixes a pre-existing gap: `GenerateIdealPathCommandHandler` (the wizard path) never writes `GoalStartDate`/`GoalEndDate` to `UserProfile`. Only the import path does. This means manual-setup users have null goal dates, contradicting the invariant that GoalStartDate is always set. This fix is a prerequisite for the trends screen and ships in the same phase.

---

## User-Confirmed Rules

| Rule | Decision |
|------|----------|
| X-axis start | `GoalStartDate` — always set (wizard or import); never null after this phase |
| X-axis end | `GoalEndDate` — always set alongside GoalStartDate |
| Weekly average | Mean of all actual weight entries recorded Mon–Sun of that calendar week |
| Weekly average anchors | Every Monday in range **+** GoalEndDate (partial last week always included) |
| Weekly loss bar | `avg(prevWeek) − avg(thisWeek)` — positive = lost weight = copper, negative = nickel |
| Y-axis | Auto-scaled to min/max of all ideal + actual data, with 5% padding |
| Actual line segments | Connect only consecutive dates (lift the pen across gaps — gaps are intentionally visible) |

---

## Visual Specification

### Evolution Chart (top)

| Series | Color | Style |
|--------|-------|-------|
| `DailyIdealWeight` — one point per day | Nickel `#9A9EAB` | Continuous line, 1.5px |
| Ideal weekly averages (Mon–Sun mean) | White `#FFFFFF` | Filled circle, 5px radius |
| `DailyActualWeight` — only recorded days | Copper `#D28B5C` | Line segments between consecutive records, 1.5px |
| Actual weekly averages (Mon–Sun mean) | Amber `#E8A838` | Filled circle, 5px radius |

**Axes:**
- X labels: "01 MMM yyyy" at the first day of each month
- Y labels: 4–6 evenly-spaced weight values on the left edge of the plot area
- Plot margins: ~40px left (Y labels), ~30px bottom (X labels), 8px top, 8px right

### Weekly Loss/Gain Chart (bottom)

- Title: "Weekly average weight loss"
- One bar per completed weekly average pair (requires two consecutive weeks with actual data)
- Bar color: copper if `DeltaKg > 0`, nickel if `DeltaKg ≤ 0`
- X labels: "d MMM" format (e.g., "5 Jan") using `WeekStart` date

---

## Technical Approach

### Evolution chart: MAUI `GraphicsView` + `IDrawable`

Microcharts does not support 4 synchronized series (two continuous lines + two dot-only overlays) sharing a single coordinate system. MAUI's built-in `GraphicsView` + `IDrawable` uses `Microsoft.Maui.Graphics` (zero extra NuGet) and gives full control over coordinate mapping — exactly what the multi-series overlay requires.

### Bar chart: Microcharts.Maui `BarChart`

Single-series bar chart where each `ChartEntry` carries its own color. Microcharts is well-suited here and satisfies the "Integration of Microcharts.Maui" technical spec requirement.

---

## Data Flow

```
TrendsPage.OnAppearing()
  → TrendsViewModel.LoadAsync()
    → mediator.Send(GetTrendsDataQuery)
      → GetTrendsDataQueryHandler
          - IUserProfileRepository.GetAsync()
          - IDailyIdealWeightRepository.GetRangeAsync(first, last)
          - IDailyActualWeightRepository.GetRangeAsync(first, last)
          - Compute ideal series, actual series
          - Compute weekly anchor dates (Mondays + GoalEndDate)
          - Compute ideal weekly averages
          - Compute actual weekly averages
          - Compute weekly deltas
          → TrendsDataDto
      ← TrendsDataDto?
    → WeightEvolutionDrawable.Update(dto)
    → WeeklyDeltaChart = BuildBarChart(dto.WeeklyDeltas)
  → TrendsPage: EvolutionView.Invalidate()
```

---

## Layer-by-Layer Changes

### 0. Prerequisite Fix — `GenerateIdealPathCommandHandler`

The wizard calls `GenerateIdealPathCommand` after setup completes. The handler currently writes only to `DailyIdealWeights` and does not touch `UserProfile`. It needs a second dependency — `IUserProfileRepository` — and must persist the goal date range after generating weights.

**`GenerateIdealPathCommand.cs`** — no parameter changes needed; handler derives dates from existing parameters.

**`GenerateIdealPathCommandHandler.cs`** — add `IUserProfileRepository`:

```csharp
public class GenerateIdealPathCommandHandler(
    IDailyIdealWeightRepository idealRepo,
    IUserProfileRepository      profileRepo)           // ← added
    : IRequestHandler<GenerateIdealPathCommand, Unit>
{
    public async Task<Unit> Handle(GenerateIdealPathCommand request, CancellationToken ct)
    {
        var totalDays = request.ExactTotalDays ?? request.TargetPeriod.TotalDays();
        // ... existing ideal weight generation unchanged ...

        // Persist goal date range so GoalStartDate/GoalEndDate are never null
        var profile = await profileRepo.GetAsync(ct);
        if (profile is not null)
        {
            profile.GoalStartDate = request.StartDate;
            profile.GoalEndDate   = request.StartDate.AddDays(totalDays - 1);
            await profileRepo.SaveAsync(profile, ct);
        }

        return Unit.Value;
    }
}
```

**Why this doesn't conflict with the import path:**  
The import calls `UpdateGoalFromImportCommand` first (sets `GoalStartDate` and `GoalEndDate`), then calls `GenerateIdealPathCommand` with `ExactTotalDays`. The math is identical: `firstDate.AddDays(totalDays - 1) == lastDate`. The overwrite produces the same values.

**New test coverage** in `GenerateIdealPathCommandHandlerTests.cs`:
- `Handle_SetsGoalStartDate_OnUserProfile`
- `Handle_SetsGoalEndDate_OnUserProfile`
- `Handle_WhenProfileIsNull_DoesNotThrow` (profile not yet created)

### 1. Domain — `IDailyIdealWeightRepository`

Add one method alongside the existing three:

```csharp
Task<IReadOnlyList<DailyIdealWeight>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
```

### 2. Application — DTOs

**`WeightManagement/DTOs/WeightPointDto.cs`**
```csharp
public sealed record WeightPointDto(DateOnly Date, double WeightKg);
```

**`WeightManagement/DTOs/WeeklyDeltaDto.cs`**
```csharp
public sealed record WeeklyDeltaDto(DateOnly WeekStart, double DeltaKg); // positive = lost weight
```

**`WeightManagement/DTOs/TrendsDataDto.cs`**
```csharp
public sealed record TrendsDataDto(
    DateOnly                           FirstDate,
    DateOnly                           LastDate,
    IReadOnlyList<WeightPointDto>      IdealSeries,
    IReadOnlyList<WeightPointDto>      ActualSeries,
    IReadOnlyList<WeightPointDto>      IdealWeeklyAverages,
    IReadOnlyList<WeightPointDto>      ActualWeeklyAverages,
    IReadOnlyList<WeeklyDeltaDto>      WeeklyDeltas
);
```

### 3. Application — `GetTrendsDataQuery`

**`Queries/GetTrendsData/GetTrendsDataQuery.cs`**
```csharp
public sealed record GetTrendsDataQuery : IRequest<TrendsDataDto?>;
```

**`Queries/GetTrendsData/GetTrendsDataQueryHandler.cs`** — full handler logic:

```
1. Load UserProfile
   - If null, or GoalStartDate/GoalEndDate is null → return null
   - firstDate = GoalStartDate.Value
   - lastDate  = GoalEndDate.Value

2. Load data
   - idealWeights  = IDailyIdealWeightRepository.GetRangeAsync(firstDate, lastDate)
   - actualWeights = IDailyActualWeightRepository.GetRangeAsync(firstDate, lastDate)
   - If idealWeights is empty → return null (ideal path not generated yet)

3. Build IdealSeries + ActualSeries directly from loaded lists

4. Compute weekly anchor dates
   - Find first Monday on-or-after firstDate
   - Collect every Monday up to and including the last Monday ≤ lastDate
   - Always append lastDate as the final anchor (if it is not already a Monday in the list)

5. For each anchor date m, compute ideal weekly average
   - weekEnd = min(m + 6 days, lastDate)
   - idealWeeklyAvg[m] = mean of ideal weights where Date ∈ [m, weekEnd]

6. For each anchor date m, compute actual weekly average
   - weekEnd = min(m + 6 days, lastDate)
   - weekActuals = actual weights where Date ∈ [m, weekEnd]
   - If weekActuals is empty → skip this anchor (no dot plotted)
   - Else actualWeeklyAvg[m] = mean of weekActuals

7. Compute WeeklyDeltas
   - Take ordered list of actual weekly averages computed in step 6
   - For i = 1 to N-1: delta = avgList[i-1].WeightKg - avgList[i].WeightKg
   - WeekStart = the anchor date of avgList[i]

8. Return TrendsDataDto
```

**Note on partial last week:**  
When `GoalEndDate` falls mid-week (e.g., a Wednesday), its anchor covers only Mon–Wed. If any actual weights exist in that partial range, they produce a weekly average dot at `GoalEndDate`. The weekly delta for the last bar uses this partial-week average as normal.

### 4. Infrastructure — `DailyIdealWeightRepository`

Add alongside existing methods:

```csharp
public async Task<IReadOnlyList<DailyIdealWeight>> GetRangeAsync(
    DateOnly from, DateOnly to, CancellationToken ct = default) =>
    await context.DailyIdealWeights
        .Where(e => e.Date >= from && e.Date <= to)
        .OrderBy(e => e.Date)
        .ToListAsync(ct);
```

### 5. MAUI — NuGet + Initialization

1. Add `Microcharts.Maui` to `LeanAI.Maui.csproj`
2. In `MauiProgram.cs`, chain `.UseSkiaSharp()` (required by Microcharts.Maui's ChartView which wraps `SKCanvasView`)

### 6. MAUI — `WeightEvolutionDrawable`

**`Views/Trends/WeightEvolutionDrawable.cs`**

```csharp
public class WeightEvolutionDrawable : IDrawable
{
    // Data — updated by ViewModel before Invalidate()
    public IReadOnlyList<WeightPointDto> IdealSeries          { get; set; } = [];
    public IReadOnlyList<WeightPointDto> ActualSeries         { get; set; } = [];
    public IReadOnlyList<WeightPointDto> IdealWeeklyAverages  { get; set; } = [];
    public IReadOnlyList<WeightPointDto> ActualWeeklyAverages { get; set; } = [];
    public DateOnly FirstDate { get; set; }
    public DateOnly LastDate  { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (IdealSeries.Count == 0) return;
        
        // 1. Compute plot area (inside margins)
        // 2. Compute yMin/yMax from all series + 5% padding
        // 3. Draw background (#222222)
        // 4. Draw Y-axis grid lines + labels (4–6 ticks, nickel color, font size 10)
        // 5. Draw X-axis month-boundary labels (nickel, font size 10)
        // 6. Draw ideal line (nickel, StrokeSize 1.5)
        // 7. Draw actual line — only segments between consecutive records (copper, StrokeSize 1.5)
        // 8. Draw ideal weekly average dots (white fill, radius 5)
        // 9. Draw actual weekly average dots (amber fill, radius 5)
    }

    // Coordinate helpers
    private float DateToX(DateOnly date, RectF plotArea) { ... }  // linear interpolation
    private float WeightToY(double kg, RectF plotArea, double yMin, double yMax) { ... }
}
```

Key coordinate mapping:
- `x = plotArea.Left + (date.DayNumber - FirstDate.DayNumber) / (float)(LastDate.DayNumber - FirstDate.DayNumber) * plotArea.Width`
- `y = plotArea.Bottom - (kg - yMin) / (yMax - yMin) * plotArea.Height`

For the actual line: iterate through `ActualSeries` (already sorted by date); draw `DrawLine(prev, curr)` only when `curr.Date == prev.Date.AddDays(1)`. When a gap exists (e.g., 3 days with no recording), lift the pen — the gap is rendered as empty space, making missing days visible to the user.

### 7. MAUI — `TrendsViewModel`

**`ViewModels/TrendsViewModel.cs`**

```csharp
public partial class TrendsViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasData;
    [ObservableProperty] private Chart? _weeklyDeltaChart;

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

            WeeklyDeltaChart = BuildWeeklyDeltaChart(dto.WeeklyDeltas);
        }
        finally { IsBusy = false; }
    }

    private static Chart BuildWeeklyDeltaChart(IReadOnlyList<WeeklyDeltaDto> deltas)
    {
        var entries = deltas.Select(d => new ChartEntry((float)d.DeltaKg)
        {
            Color     = d.DeltaKg >= 0 ? SKColor.Parse("#D28B5C") : SKColor.Parse("#9A9EAB"),
            Label     = d.WeekStart.ToString("d MMM"),
            TextColor = SKColor.Parse("#9A9EAB"),
        }).ToArray();

        return new BarChart
        {
            Entries         = entries,
            BackgroundColor = SKColor.Parse("#222222"),
            LabelTextSize   = 24,
        };
    }
}
```

Register in `MauiProgram.cs`:
```csharp
builder.Services.AddTransient<TrendsViewModel>();
```

### 8. MAUI — `TrendsPage.xaml`

No `ScrollView`. A three-row `Grid` fills the full screen:

- Row 0 `Auto` — page title "Trends" + loading spinner  
- Row 1 `*` — evolution chart Border (title + `GraphicsView` fill remaining height)  
- Row 2 `Auto` — weekly bar chart Border (sized by content)

The evolution chart title **"Actual weight vs. Ideal Weight"** lives inside the Border in a nested `Grid` (`Auto` title row, `*` `GraphicsView` row). `HeightRequest` is removed from `GraphicsView` — it expands to fill whatever Row 1 provides.

```xml
<Grid Padding="16,24,16,16" RowSpacing="12">
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
  </Grid.RowDefinitions>

  <!-- Row 0: page header -->
  <VerticalStackLayout Grid.Row="0" Spacing="6">
    <Label Text="Trends" FontSize="22" FontAttributes="Bold" TextColor="{StaticResource ColorText}" />
    <ActivityIndicator IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}"
                       Color="{StaticResource ColorCopper}" HorizontalOptions="Start" />
  </VerticalStackLayout>

  <!-- Row 1: evolution chart (fills remaining height) -->
  <Border Grid.Row="1" IsVisible="{Binding HasData}"
          Stroke="{StaticResource ColorCopper}" StrokeThickness="1" Padding="8,6,8,4">
    <Border.StrokeShape><RoundRectangle CornerRadius="4" /></Border.StrokeShape>
    <Grid RowSpacing="4">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
      </Grid.RowDefinitions>
      <Label Grid.Row="0" Text="Actual weight vs. Ideal Weight"
             TextColor="{StaticResource ColorNickel}" FontSize="13" />
      <GraphicsView Grid.Row="1" x:Name="EvolutionView" Drawable="{Binding EvolutionDrawable}" />
    </Grid>
  </Border>

  <!-- Row 1: empty state (shown when HasData = false) -->
  <Label Grid.Row="1"
         IsVisible="{Binding HasData, Converter={StaticResource InverseBoolConverter}}"
         Text="No data yet — complete setup and start logging your weight to see trends."
         TextColor="{StaticResource ColorNickel}" FontSize="14"
         HorizontalTextAlignment="Center" HorizontalOptions="Center" VerticalOptions="Center" />

  <!-- Row 2: weekly bar chart -->
  <Border Grid.Row="2" IsVisible="{Binding HasData}"
          Stroke="{StaticResource ColorCopper}" StrokeThickness="1" Padding="8,6,8,4">
    <Border.StrokeShape><RoundRectangle CornerRadius="4" /></Border.StrokeShape>
    <VerticalStackLayout Spacing="4">
      <Label Text="Weekly average weight loss" TextColor="{StaticResource ColorNickel}" FontSize="13" />
      <microcharts:ChartView Chart="{Binding WeeklyDeltaChart}" HeightRequest="160" />
    </VerticalStackLayout>
  </Border>
</Grid>
```

**`TrendsPage.xaml.cs`** — Code-behind:

```csharp
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
```

---

## Tests

**File:** `tests/LeanAI.Tests/Application/WeightManagement/GenerateIdealPathCommandHandlerTests.cs` — add to existing:

| Test | What it verifies |
|------|-----------------|
| `Handle_SetsGoalStartDate_OnUserProfile` | `UserProfile.GoalStartDate == request.StartDate` after handler runs |
| `Handle_SetsGoalEndDate_OnUserProfile` | `UserProfile.GoalEndDate == request.StartDate.AddDays(totalDays - 1)` |
| `Handle_WhenProfileIsNull_DoesNotThrow` | Handler completes cleanly when `profileRepo.GetAsync` returns null |

---

**File:** `tests/LeanAI.Tests/Application/WeightManagement/GetTrendsDataQueryHandlerTests.cs` — new file:

| Test | What it verifies |
|------|-----------------|
| `Handle_Returns_Null_When_ProfileIsNull` | Null when no UserProfile exists |
| `Handle_Returns_Null_When_GoalDatesNotSet` | Null when GoalStartDate or GoalEndDate is null |
| `Handle_Returns_Null_When_IdealWeightTableEmpty` | Null when GoalDates are set but no ideal rows exist |
| `Handle_Returns_Correct_FirstAndLastDate` | FirstDate = GoalStartDate, LastDate = GoalEndDate |
| `Handle_IdealSeries_Contains_AllIdealRows` | IdealSeries.Count equals ideal weight rows in range |
| `Handle_ActualSeries_Contains_OnlyRecordedDays` | ActualSeries excludes days with no recording |
| `Handle_WeeklyAverage_IsMeanOfMonToSun` | 3 entries on Mon/Tue/Wed → weekly avg = their arithmetic mean |
| `Handle_PartialLastWeek_IsIncludedAsAnchor` | GoalEndDate on Wednesday still produces an actual weekly average point |
| `Handle_IdealWeeklyAverage_ComputedForEachAnchor` | Ideal weekly averages count = Monday count + 1 (for GoalEndDate if not Monday) |
| `Handle_WeeklyDelta_Positive_WhenWeightDecreases` | avg(week1) > avg(week2) → DeltaKg > 0 |
| `Handle_WeeklyDelta_Negative_WhenWeightIncreases` | avg(week1) < avg(week2) → DeltaKg < 0 |
| `Handle_WeeklyDeltas_Count_IsOneFewerThanAvgPoints` | N actual weekly averages → N-1 deltas |
| `Handle_SkipsWeeklyDelta_WhenWeekHasNoActualData` | Week with no recordings is skipped — not counted in delta series |

---

## New / Modified Files

| Layer | File | Change |
|-------|------|--------|
| Application | `Commands/GenerateIdealPath/GenerateIdealPathCommandHandler.cs` | + `IUserProfileRepository` dependency; write GoalStartDate/GoalEndDate |
| Domain | `IDailyIdealWeightRepository.cs` | + `GetRangeAsync` |
| Application | `DTOs/WeightPointDto.cs` | **New** |
| Application | `DTOs/WeeklyDeltaDto.cs` | **New** |
| Application | `DTOs/TrendsDataDto.cs` | **New** |
| Application | `Queries/GetTrendsData/GetTrendsDataQuery.cs` | **New** |
| Application | `Queries/GetTrendsData/GetTrendsDataQueryHandler.cs` | **New** |
| Infrastructure | `Repositories/DailyIdealWeightRepository.cs` | + `GetRangeAsync` |
| MAUI | `LeanAI.Maui.csproj` | + `Microcharts.Maui` NuGet |
| MAUI | `MauiProgram.cs` | + `.UseSkiaSharp()`, register `TrendsViewModel` |
| MAUI | `Views/Trends/WeightEvolutionDrawable.cs` | **New** |
| MAUI | `ViewModels/TrendsViewModel.cs` | **New** |
| MAUI | `Views/Trends/TrendsPage.xaml` | Replace placeholder |
| MAUI | `Views/Trends/TrendsPage.xaml.cs` | New code-behind logic |
| Tests | `GenerateIdealPathCommandHandlerTests.cs` | + 3 tests for goal date persistence |
| Tests | `GetTrendsDataQueryHandlerTests.cs` | **New** — 13 tests |

No EF Core migration required — no schema changes.

---

## Implementation Order (as executed)

1. ✅ Prerequisite fix — tests first: 3 failing tests added to `GenerateIdealPathCommandHandlerTests.cs`
2. ✅ Prerequisite fix — implemented: `IUserProfileRepository` added to handler; GoalStartDate/GoalEndDate persisted
3. ✅ Domain: `GetRangeAsync` added to `IDailyIdealWeightRepository`
4. ✅ TDD: All 13 `GetTrendsDataQueryHandlerTests` written (failing stub)
5. ✅ Application: `WeightPointDto`, `WeeklyDeltaDto`, `TrendsDataDto` created
6. ✅ Application: `GetTrendsDataQuery` + `GetTrendsDataQueryHandler` implemented → all 13 tests green
7. ✅ Infrastructure: `GetRangeAsync` implemented in `DailyIdealWeightRepository`
8. ✅ MAUI: `Microcharts.Maui` 1.0.1 added; `SkiaSharp.Views.Maui.Controls.Hosting` using + `.UseSkiaSharp()` in `MauiProgram.cs`
9. ✅ MAUI: `WeightEvolutionDrawable` created
10. ✅ MAUI: `TrendsViewModel` created and registered
11. ✅ MAUI: `TrendsPage.xaml` replaced (Grid layout, chart title, full-screen fill)
12. ✅ `dotnet test` — 95 / 95 passing, 0 build errors

---

## Scope Changes from Original Plan

| # | What changed | Why |
|---|-------------|-----|
| SC-1 | `ScrollView + VerticalStackLayout` replaced with full-screen `Grid` | User requested charts fill the screen |
| SC-2 | Evolution chart title "Actual weight vs. Ideal Weight" added | User request; placed inside the Border above the `GraphicsView` |
| SC-3 | `GraphicsView` has no `HeightRequest` — expands via `Height="*"` row | Required by full-screen layout |
| SC-4 | `SkiaSharp.Views.Maui.Controls.Hosting` using required in `MauiProgram.cs` | `.UseSkiaSharp()` not found without it |

---

## Resolved Risks

| # | Risk | Outcome |
|---|------|---------|
| R1 | Microcharts `BarChart` negative values | Resolved — negative entries render as downward bars natively |
| R2 | `GraphicsView.Invalidate()` threading | Resolved — called on main thread in `OnAppearing` after `await` |
| R3 | Large dataset draw performance | Not an issue for line segment drawing at scale |
| R4 | `InverseBoolConverter` availability | Already registered in `App.xaml` as `InverseBoolConverter` |
