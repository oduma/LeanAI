# Phase 6: History Hub — Implementation Plan

**Status:** Done ✅  
**Target:** New Calendar tab with gamified monthly achievement matrix

---

## Key Design Decisions (from Q&A)

| # | Question | Decision |
|---|----------|----------|
| D1 | Past in-range days with no record | Dimmed but **clickable** — opens new-entry LogPage |
| D2 | Future in-range days | Dimmed and **non-clickable** |
| D3 | Halo on first-ever recorded day | **Copper** (optimistic default — no prior day to compare against) |
| D4 | Weekly averages storage | **New `WeeklyAverages` DB table**; updated on every `UpsertDailyLogCommand` |
| D5 | First-day-of-week effect on averages | **None** — averages always computed Mon–Sun; `CalendarFirstDay` changes column display only |
| D6 | Default `CalendarFirstDay` | **Monday** |
| D7 | Tab order | **Log \| Trends \| Calendar \| Settings** |
| D8 | Calendar date bound navigation | Free month navigation; all cells outside `GoalStartDate`–`GoalEndDate` are dimmed and non-clickable |
| D9 | LogPage for calendar dates | Pushed **modally** from CalendarPage — consistent with Wizard pattern; avoids Shell tab routing conflict |

---

## Architecture Overview

```
Domain          WeeklyAverage entity + IWeeklyAverageRepository
                AppSettings.CalendarFirstDay (new field)

Application     RecalculateWeeklyAverageCommand  (hooked into UpsertDailyLog)
                GetCalendarMonthQuery
                AppSettingsDto / Query / Command updated for CalendarFirstDay

Infrastructure  WeeklyAverageRepository (EF Core)
                Migration: Add_WeeklyAverages_And_CalendarFirstDay

Presentation    CalendarDayViewModel / CalendarViewModel
                CalendarPage.xaml (new tab)
                LogViewModel updated for date parameter (modal reuse)
                SettingsPage updated for CalendarFirstDay
                AppShell.xaml: new Calendar ShellContent
```

---

## 1. Domain Layer

### 1.1 New Entity: `WeeklyAverage`

**File:** `src/LeanAI.Domain/WeightManagement/Entities/WeeklyAverage.cs`

```csharp
public class WeeklyAverage : BaseEntity
{
    /// <summary>Always the Monday of the Mon–Sun week.</summary>
    public DateOnly WeekStart    { get; set; }
    public double   AverageWeightKg { get; set; }
}
```

### 1.2 New Interface: `IWeeklyAverageRepository`

**File:** `src/LeanAI.Domain/WeightManagement/Interfaces/IWeeklyAverageRepository.cs`

```csharp
public interface IWeeklyAverageRepository
{
    Task<IReadOnlyList<WeeklyAverage>> GetRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default);
    Task UpsertAsync(WeeklyAverage entry, CancellationToken ct = default);
}
```

### 1.3 Update `AppSettings` Entity

Add one new property:

```csharp
/// <summary>First calendar column. Monday (1) or Sunday (0). Default Monday.</summary>
public DayOfWeek CalendarFirstDay { get; set; } = DayOfWeek.Monday;
```

---

## 2. EF Core Migration

**Migration name:** `Add_WeeklyAverages_And_CalendarFirstDay`

Changes:
- **New table `WeeklyAverages`:**
  - `Id` — TEXT (Guid) PRIMARY KEY
  - `WeekStart` — TEXT NOT NULL UNIQUE  (DateOnly stored as "yyyy-MM-dd")
  - `AverageWeightKg` — REAL NOT NULL
- **Alter table `AppSettings`:** add column `CalendarFirstDay` INTEGER NOT NULL DEFAULT 1
  (1 = Monday, 0 = Sunday — matches `(int)DayOfWeek.Monday` / `(int)DayOfWeek.Sunday`)

No existing data is affected.

---

## 3. Infrastructure Layer

### 3.1 `WeeklyAverageRepository`

**File:** `src/LeanAI.Infrastructure/Repositories/WeeklyAverageRepository.cs`

```csharp
public sealed class WeeklyAverageRepository(LeanAIDbContext context) : IWeeklyAverageRepository
{
    public async Task<IReadOnlyList<WeeklyAverage>> GetRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await context.WeeklyAverages
            .Where(e => e.WeekStart >= from && e.WeekStart <= to)
            .OrderBy(e => e.WeekStart)
            .ToListAsync(ct);

    public async Task UpsertAsync(WeeklyAverage entry, CancellationToken ct = default)
    {
        var existing = await context.WeeklyAverages
            .FirstOrDefaultAsync(e => e.WeekStart == entry.WeekStart, ct);
        if (existing is null)
            context.WeeklyAverages.Add(entry);
        else
            existing.AverageWeightKg = entry.AverageWeightKg;
        await context.SaveChangesAsync(ct);
    }
}
```

### 3.2 DI Registration

In `DependencyInjection.cs`, add:
```csharp
services.AddScoped<IWeeklyAverageRepository, WeeklyAverageRepository>();
```

---

## 4. Application Layer

### 4.1 New Command: `RecalculateWeeklyAverageCommand`

**File:** `src/LeanAI.Application/WeightManagement/Commands/RecalculateWeeklyAverage/RecalculateWeeklyAverageCommand.cs`

```csharp
/// <param name="Date">Any date within the target Mon–Sun week.</param>
public sealed record RecalculateWeeklyAverageCommand(DateOnly Date) : IRequest<Unit>;
```

**Handler** (`RecalculateWeeklyAverageCommandHandler.cs`):

```csharp
public sealed class RecalculateWeeklyAverageCommandHandler(
    IDailyActualWeightRepository actualRepo,
    IWeeklyAverageRepository     weeklyRepo)
    : IRequestHandler<RecalculateWeeklyAverageCommand, Unit>
{
    public async Task<Unit> Handle(RecalculateWeeklyAverageCommand request, CancellationToken ct)
    {
        // Always use Mon–Sun window regardless of CalendarFirstDay
        var monday = GetMonday(request.Date);
        var sunday = monday.AddDays(6);

        var entries = await actualRepo.GetRangeAsync(monday, sunday, ct);
        if (entries.Count == 0) return Unit.Value; // No entries → leave existing average (or it never existed)

        var avg = entries.Average(e => e.WeightKg);
        await weeklyRepo.UpsertAsync(
            new WeeklyAverage { WeekStart = monday, AverageWeightKg = avg }, ct);

        return Unit.Value;
    }

    private static DateOnly GetMonday(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysFromMonday);
    }
}
```

### 4.2 Hook into `UpsertDailyLogCommandHandler`

Extend the existing handler to inject `IWeeklyAverageRepository` and perform inline weekly average recalculation after the daily upsert. This avoids adding `IMediator` to the handler while keeping the logic testable:

```csharp
public sealed class UpsertDailyLogCommandHandler(
    IDailyActualWeightRepository dailyRepo,
    IWeeklyAverageRepository     weeklyRepo)
    : IRequestHandler<UpsertDailyLogCommand>
{
    public async Task Handle(UpsertDailyLogCommand request, CancellationToken ct)
    {
        // ... existing upsert logic unchanged ...

        // After upsert: recalculate weekly average for the affected week
        var monday = GetMonday(request.Date);
        var sunday = monday.AddDays(6);
        var weekEntries = await dailyRepo.GetRangeAsync(monday, sunday, ct);
        if (weekEntries.Count > 0)
        {
            var avg = weekEntries.Average(e => e.WeightKg);
            await weeklyRepo.UpsertAsync(
                new WeeklyAverage { WeekStart = monday, AverageWeightKg = avg }, ct);
        }
    }

    private static DateOnly GetMonday(DateOnly date)
    {
        var daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysFromMonday);
    }
}
```

### 4.3 New DTOs

**`CalendarDayState` enum** (`DTOs/CalendarDayState.cs`):
```csharp
public enum CalendarDayState
{
    OutOfRange,      // Before GoalStartDate or after GoalEndDate
    FutureInRange,   // After today but within goal period
    PastNoRecord,    // On/before today, within goal period, no weight logged
    PastHasRecord,   // On/before today, within goal period, weight recorded
}
```

**`HaloColor` enum** (`DTOs/HaloColor.cs`):
```csharp
public enum HaloColor { None, Copper, Nickel }
```

**`TrendColor` enum** (`DTOs/TrendColor.cs`):
```csharp
public enum TrendColor { None, Copper, Nickel }
```

**`CalendarDayDto`** (`DTOs/CalendarDayDto.cs`):
```csharp
public sealed record CalendarDayDto(
    DateOnly        Date,
    bool            IsInDisplayedMonth,   // False for overflow days from adjacent months
    CalendarDayState State,
    HaloColor       Halo,
    TrendColor      TextTrend,
    double?         WeightKg
);
```

**`CalendarMonthDto`** (`DTOs/CalendarMonthDto.cs`):
```csharp
public sealed record CalendarMonthDto(
    int                          Year,
    int                          Month,
    DateOnly                     GoalStart,
    DateOnly                     GoalEnd,
    DayOfWeek                    FirstDayOfWeek,
    IReadOnlyList<CalendarDayDto> Days      // Always a multiple of 7 (complete weeks)
);
```

### 4.4 New Query: `GetCalendarMonthQuery`

**`GetCalendarMonthQuery.cs`:**
```csharp
public sealed record GetCalendarMonthQuery(int Year, int Month) : IRequest<CalendarMonthDto?>;
```

**`GetCalendarMonthQueryHandler.cs`** — key logic:

```
1. Load UserProfile → null or missing GoalStartDate/GoalEndDate → return null
2. Load AppSettings → CalendarFirstDay
3. Compute grid bounds:
     gridStart = first occurrence of CalendarFirstDay on/before the 1st of (Year, Month)
     gridEnd   = last occurrence of (CalendarFirstDay - 1) on/after the last day of (Year, Month)
   (always produces complete 7-day rows)
4. Load actual weights:   IDailyActualWeightRepository.GetRangeAsync(gridStart, gridEnd)
5. Load weekly averages:
     - Compute the Monday of gridStart's week through the Monday of gridEnd's week
     - IWeeklyAverageRepository.GetRangeAsync(firstMonday, lastMonday)
6. Build actualByDate dictionary; build weeklyAvgByMonday dictionary
7. Compute "previous recorded weight" list:
     - Sort all actual entries by date; build a map date → previousActualWeightKg
     - The first recorded date maps to null (→ Copper halo by D3)
8. For each date in [gridStart, gridEnd]:
     a. Determine State (see CalendarDayState logic below)
     b. Determine Halo (only PastHasRecord gets a halo)
     c. Determine TextTrend (only PastHasRecord gets a text colour)
     d. Build CalendarDayDto
9. Return CalendarMonthDto
```

**CalendarDayState logic:**
```
date < GoalStartDate || date > GoalEndDate → OutOfRange
date > today                               → FutureInRange  (even if within goal period)
!actualByDate.ContainsKey(date)            → PastNoRecord
else                                       → PastHasRecord
```

**Halo logic (PastHasRecord only):**
```
previousWeightKg is null             → Copper  (first ever recording, D3)
currentWeight < previousWeight       → Copper  (lost weight)
currentWeight >= previousWeight      → Nickel  (gained or same)
```

**TextTrend logic (PastHasRecord only):**
```
mondayOfThisWeek = GetMonday(date)
thisWeekAvg  = weeklyAvgByMonday[mondayOfThisWeek]  (exists — week has record)
prevWeekAvg  = weeklyAvgByMonday[mondayOfThisWeek.AddDays(-7)]  (may be absent)
if prevWeekAvg is absent → None  (not enough history to color)
elif thisWeekAvg < prevWeekAvg → Copper
else → Nickel
```

### 4.5 Update AppSettings DTO / Query / Command

**`AppSettingsDto`:** add `DayOfWeek CalendarFirstDay`

**`GetAppSettingsQueryHandler`:** read `appSettings.CalendarFirstDay`

**`SaveAppSettingsCommand`:** add `DayOfWeek CalendarFirstDay` parameter

**`SaveAppSettingsCommandHandler`:** persist `CalendarFirstDay` to the entity

---

## 5. Presentation Layer

### 5.1 `CalendarDayViewModel`

**File:** `src/LeanAI.Maui/ViewModels/CalendarDayViewModel.cs`

Properties driven from `CalendarDayDto`:
```
DateOnly Date
int DayNumber               (Date.Day)
bool IsInDisplayedMonth
bool IsClickable            (State == PastHasRecord || State == PastNoRecord)
Color HaloColor             (Copper / Nickel / Transparent)
bool HasHalo                (State == PastHasRecord)
Color DayNumberColor        (Copper / Nickel / dimmed white based on TrendColor / State)
double Opacity              (1.0 for active days, 0.35 for out-of-range/future)
double? WeightKg
```

### 5.2 `CalendarViewModel`

**File:** `src/LeanAI.Maui/ViewModels/CalendarViewModel.cs`

```csharp
public partial class CalendarViewModel(IMediator mediator) : ObservableObject
{
    [ObservableProperty] private int    _currentYear;
    [ObservableProperty] private int    _currentMonth;
    [ObservableProperty] private string _monthLabel = string.Empty;    // "May 2026"
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private bool   _hasData;
    [ObservableProperty] private bool   _canGoToPrevMonth;
    [ObservableProperty] private bool   _canGoToNextMonth;

    public ObservableCollection<CalendarDayViewModel>  Days         { get; } = new();
    public ObservableCollection<string>                ColumnHeaders { get; } = new(); // day-of-week labels

    [RelayCommand] Task PreviousMonthAsync();
    [RelayCommand] Task NextMonthAsync();
    [RelayCommand] Task DayTappedAsync(CalendarDayViewModel day);  // guard: only clickable days
    Task LoadMonthAsync(int year, int month);
}
```

**Navigation guard for `DayTappedAsync`:** if `!day.IsClickable` → no-op.

**Modal navigation for `DayTappedAsync`:**
```csharp
var logVm = serviceProvider.Resolve<LogViewModel>();
await logVm.LoadForDateAsync(day.Date);
var page  = new LogPage(logVm);
await Shell.Current.Navigation.PushModalAsync(page);
```

### 5.3 Update `LogViewModel` for Date Parameter

Add a new public entry point (alongside the existing `LoadLogAsync`):

```csharp
public async Task LoadForDateAsync(DateOnly date, CancellationToken ct = default)
{
    EntryDate      = date;
    EntryDateLabel = date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    // ... same body as LoadLogAsync but uses the passed date instead of today ...
}
```

The existing `LoadLogAsync` remains unchanged (called from `LogPage.OnAppearing` for the tab flow).

> **Note:** When LogPage is opened modally from the Calendar, `OnAppearing` still fires. The ViewModel must not re-load to today's date in that case. A flag `_dateWasExplicitlySet` set by `LoadForDateAsync` guards this.

### 5.4 `CalendarPage.xaml`

**File:** `src/LeanAI.Maui/Views/Calendar/CalendarPage.xaml`

Layout structure:
```
ContentPage
  Grid (rows: Auto, Auto, *)
    Row 0 — "Calendar" page title + ActivityIndicator
    Row 1 — Month navigation bar: [<] "May 2026" [>]
    Row 2 — Calendar grid:
               Row 0 — 7 column headers (Mon Tue … Sun, or Sun Mon … Sat)
               Row 1+ — CollectionView (ItemsLayout=GridItemsLayout, Span=7)
                         Each item = CalendarCell (see below)
```

**CalendarCell template** (DataTemplate bound to `CalendarDayViewModel`):
```
Grid (fixed size, e.g. 44×44)
  Ellipse (halo circle, IsVisible={Binding HasHalo}, Stroke={Binding HaloColor}, StrokeThickness=2)
  Label  (day number, TextColor={Binding DayNumberColor}, Opacity={Binding Opacity})
```

**Empty state:** When `HasData = false` (no goal set), show a centered message in Row 2.

### 5.5 Update `SettingsPage.xaml` and `SettingsViewModel`

Add a new "Calendar" section in `SettingsPage.xaml`:

```
Label "First Day of Week"
Picker with items: "Monday", "Sunday"
```

`SettingsViewModel`:
- `[ObservableProperty] string CalendarFirstDay`  — bound to Picker selection
- `SaveAiSettingsAsync` already calls `SaveAppSettingsCommand`; extend the command to include `CalendarFirstDay`

### 5.6 Update `AppShell.xaml`

Add Calendar ShellContent between Trends and Settings:
```xml
<ShellContent
    Title="Calendar"
    Icon="calendar_tab.svg"
    ContentTemplate="{DataTemplate calendar:CalendarPage}"
    Route="Calendar" />
```

Add new SVG icon file: `src/LeanAI.Maui/Resources/Images/calendar_tab.svg`

---

## 6. Implementation Order (TDD)

Each step: write failing tests → implement → green → next.

| Step | Layer | Work |
|------|-------|------|
| 1 | Domain | `WeeklyAverage` entity; `IWeeklyAverageRepository`; `AppSettings.CalendarFirstDay` |
| 2 | Infrastructure | EF Core migration `Add_WeeklyAverages_And_CalendarFirstDay` |
| 3 | Infrastructure | `WeeklyAverageRepository`; register in DI |
| 4 | Application | **Tests** + `RecalculateWeeklyAverageCommandHandler` |
| 5 | Application | **Tests** + extend `UpsertDailyLogCommandHandler` (inject `IWeeklyAverageRepository`, recalc inline); update existing handler tests |
| 6 | Application | **Tests** + update `AppSettings` DTO / `GetAppSettingsQueryHandler` / `SaveAppSettingsCommandHandler` for `CalendarFirstDay` |
| 7 | Application | **Tests** + `GetCalendarMonthQueryHandler` (bulk of logic — all cell states, halo, trend, boundary conditions) |
| 8 | Presentation | `CalendarDayViewModel`, `CalendarViewModel`, `CalendarPage.xaml`, `CalendarPage.xaml.cs` |
| 9 | Presentation | Update `LogViewModel` (`LoadForDateAsync`, modal guard flag); update `LogPage.xaml.cs` |
| 10 | Presentation | Update `SettingsPage.xaml` + `SettingsViewModel` for `CalendarFirstDay` picker |
| 11 | Presentation | Add `calendar_tab.svg`; update `AppShell.xaml` |
| 12 | Validation | `dotnet build` → 0 errors, 0 warnings; `dotnet test` → all green |

---

## 7. Test Plan (Application Layer)

### `RecalculateWeeklyAverageCommandHandlerTests`
- Single entry in week → average equals that entry
- Multiple entries → correct mean
- Date on Monday → same-week entries included
- Date on Sunday → same-week entries included
- No entries in week → no upsert called

### `UpsertDailyLogCommandHandlerTests` (updates to existing)
- Upsert triggers weekly average recalculation for the correct week
- Constructor now accepts `IWeeklyAverageRepository` mock

### `GetAppSettingsQueryHandlerTests` / `SaveAppSettingsCommandHandlerTests` (updates)
- `CalendarFirstDay` is returned / persisted correctly
- Default (Monday) when no DB row exists

### `GetCalendarMonthQueryHandlerTests`
- Returns null when UserProfile is missing
- Returns null when GoalStartDate / GoalEndDate are null
- Grid always has a multiple-of-7 cell count
- Cells before GoalStartDate → OutOfRange
- Cells after GoalEndDate → OutOfRange
- Future cells within goal period → FutureInRange, not clickable
- Today within goal, no record → PastNoRecord, clickable
- Today within goal, has record → PastHasRecord, has halo
- First recorded day → Copper halo
- Weight loss day → Copper halo
- Weight gain day → Nickel halo
- Week avg lower than prev → Copper text
- Week avg higher than prev → Nickel text
- First week (no prior week avg) → TextTrend = None
- Column headers shift correctly with CalendarFirstDay = Sunday
- Grid start/end respects CalendarFirstDay setting
- Days from adjacent months appear with `IsInDisplayedMonth = false`

---

## 8. Risks & Mitigations

| # | Risk | Mitigation |
|---|------|-----------|
| R1 | `LogViewModel` modal reuse — `OnAppearing` re-loads today | Add `_dateWasExplicitlySet` guard flag; `LoadForDateAsync` sets it before modal push |
| R2 | Historical import data has no weekly averages | After import, dispatch `RecalculateWeeklyAverageCommand` for every affected week in `ImportGoogleSheetsCommandHandler` |
| R3 | `CalendarFirstDay` migration default must match `DayOfWeek.Monday` (= 1) | Verify SQLite DEFAULT value in migration; test with a clean DB |
| R4 | CollectionView with 42 items + data binding — perf on first load | Use `RecycleElement` strategy; `CalendarDayViewModel` properties are simple value types |
| R5 | EF Core `DateOnly` comparison for `WeekStart UNIQUE` constraint | Confirm EF Core 10 stores `DateOnly` as TEXT "yyyy-MM-dd"; existing pattern from `DailyActualWeight.Date` confirms this works |
| R6 | Re-ordering the CalendarFirstDay column may show wrong week-start for averages | Weekly averages are keyed on Monday regardless of setting; query uses Monday keys; display-only shift in column headers — no data correctness risk |

---

## 9. Definition of Done

- [x] Monthly calendar grid renders with correct week layout for both Monday-start and Sunday-start.
- [x] Cells outside `GoalStartDate`–`GoalEndDate` are dimmed (35% opacity) and non-interactive.
- [x] Future in-range cells are dimmed and non-interactive.
- [x] Past in-range cells with no record are dimmed but tappable → opens LogPage modal in new-entry mode.
- [x] Past in-range cells with a record are fully active, show halo and coloured day number.
- [x] Halo = Copper for first recorded day and weight-loss days; Nickel for same/gain days.
- [x] Day number = Copper when week avg < previous week avg; Nickel otherwise; no colour when no prior week.
- [x] `WeeklyAverages` table updated on every daily weight save (current day and historical edits from calendar).
- [x] `WeeklyAverages` table updated after Google Sheets import (Phase 3 of `ImportGoogleSheetsCommandHandler`).
- [x] Settings shows "First Day of Week" picker; changing it persists to DB and Calendar tab re-renders.
- [x] Calendar cells fill available vertical screen height dynamically (`CellHeight` observable, `FontSize=20`, halo 50×50).
- [x] All Phase 5 and earlier tests continue to pass.
- [x] New Application-layer handlers tested at 100% branch coverage.
- [x] `dotnet build` → 0 errors, 0 warnings. `dotnet test` → 123 tests green.

---

## 10. Post-Implementation Notes

### P1 — Risk R2 resolution: Weekly averages after Google Sheets import

**Plan said:** Dispatch `RecalculateWeeklyAverageCommand` for every affected week in `ImportGoogleSheetsCommandHandler`.

**Actual implementation:** Rather than dispatching per-week commands (which would each trigger extra DB reads), a Phase 3 block was added directly inside `ImportGoogleSheetsCommandHandler.Handle()`. After Phase 2 writes all rows, all written entries are combined (`existingEntries + toInsert`) and grouped by `GetMonday(date)`. One `weeklyRepo.UpsertAsync` call is made per distinct Monday — no additional DB reads required. `IWeeklyAverageRepository` is injected into the constructor alongside the existing dependencies.

### P2 — Calendar cell sizing (post-launch UX fix)

**Original plan:** Fixed cell size (e.g. 44×44).

**Actual implementation:** Dynamic height binding so cells fill the full available screen height:
- `CalendarViewModel` gains `[ObservableProperty] double _cellHeight = 70` and `private int _weekRowCount`.
- `RefreshAsync` sets `_weekRowCount = Days.Count / 7` after building the collection, then calls `RecalculateCellHeight()`.
- `CalendarPage.xaml.cs` subscribes to `CalendarGrid.SizeChanged`; the handler calls `_vm.UpdateGridHeight(CalendarGrid.Height)`, which recomputes `CellHeight = gridHeight / _weekRowCount`.
- Each cell's `HeightRequest` is bound via `RelativeSource AncestorType=CalendarViewModel`.
- `FontSize` increased 14 → 20 (Bold); halo ellipse increased 38 → 50px; `StrokeThickness` 2 → 2.5.
