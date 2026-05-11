# Phase 4: Daily Tracking & Feedback — Implementation Plan ✅ COMPLETE

## Goal
Implement the **Log tab** as a full Daily Entry screen where users record their weight with optional comments, and receive instant real-time feedback (yesterday delta, ideal comparison, weekly projection) using a 500 ms autosave debounce.

---

## Mockup Reference
`z-com-ai/Data Entry with on the spot analysis.png`

Key observations:
- Header: "Daily Entry | 10 May 2026"
- Large weight input field with Copper border, unit label below
- Optional multi-line Comments entry
- "Real-time Analysis" section with two side-by-side cards: Yesterday Delta | Predicted Weekly Loss
- Full-width warning box (Copper border/background) when weight > ideal
- Autosaving spinner + "Data is debounced and saved automatically" footer

---

## Design Decisions

### Color Logic (global spec applies)
| Situation | Color |
|---|---|
| Yesterday delta — weight **lost** (negative delta) | **Copper** (#D28B5C) — on-track, positive |
| Yesterday delta — weight **gained** (positive delta) | **Nickel** (#9A9EAB) — off-track, warning |
| Weight **≤ ideal** today | No warning shown |
| Weight **> ideal** today | Copper warning box (highest-energy color in palette, "high-contrast" per UI_UX_SPEC §7) |

### Unit Handling
- All DB storage and Application layer math: **metric (kg)**.
- ViewModel converts kg ↔ display units (`UserProfile.UnitSystem`) before display and after input parse.
- Conversion: `lb = kg × 2.20462` / `kg = lb / 2.20462`.
- Display precision: 1 decimal place.

### Log Tab Behavior
- Log tab **always** shows the Daily Entry screen (entry exists → pre-filled for editing, entry missing → empty form).
- "Autoload": on app launch, if today has no entry, programmatically navigate to the Log tab so the user immediately sees the blank form.

### Analysis Calculations (all performed in the ViewModel)

**1. Yesterday Delta**
```
delta = YesterdayWeightKg - CurrentWeightKg
```
- Negative delta (lost weight) → display e.g. `−0.4 kg`, Copper color
- Positive delta (gained weight) → display e.g. `+0.4 kg`, Nickel color
- No yesterday entry → display `—` (both cards shown but with dash)

**2. Ideal Warning**
```
isAboveIdeal = CurrentWeightKg > TodayIdealWeightKg
aboveIdealDelta = CurrentWeightKg - TodayIdealWeightKg
```
Warning box visible only when `isAboveIdeal == true`.

**3. Projected Weekly Loss (Current)**
```
weekLoss = WeekFirstWeightKg - CurrentWeightKg   (total lost since start of week)
projectedWeekly = weekLoss / WeekDaysLoggedThisWeek × 7
```
- `WeekFirstWeightKg`: weight of the **earliest logged entry** in the current ISO week (Mon–Sun).
- `WeekDaysLoggedThisWeek`: count of DB entries for the current ISO week. On first type today (before first save), ViewModel adds +1 to represent today's live entry.
- Display `—` if `WeekDaysLoggedThisWeek < 2` (insufficient data for a meaningful projection).

**4. Ideal Weekly Loss**
```
IdealWeeklyLossKg = (StartingWeightKg - TargetWeightKg) / TotalDays × 7
```
Sourced from `UserProfile`. Display `—` if profile incomplete or null.

### Autosave Debounce
500 ms `CancellationTokenSource` debounce on both `WeightDisplayText` and `Notes` changes. A single `SaveLogCommand` upserts both fields at once.

---

## Implementation Steps

### Step 1 — Domain: `IDailyActualWeightRepository`

**New file:** `src/LeanAI.Domain/WeightManagement/Interfaces/IDailyActualWeightRepository.cs`
```csharp
Task<DailyActualWeight?> GetByDateAsync(DateOnly date, CancellationToken ct = default);
Task<IReadOnlyList<DailyActualWeight>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
Task UpsertAsync(DailyActualWeight entry, CancellationToken ct = default);
```

**Extend** `IDailyIdealWeightRepository` with:
```csharp
Task<DailyIdealWeight?> GetByDateAsync(DateOnly date, CancellationToken ct = default);
```

---

### Step 2 — Application: DTOs

**New file:** `src/LeanAI.Application/WeightManagement/DTOs/LogContextDto.cs`
```csharp
public record LogContextDto(
    double?    TodayWeightKg,        // null → no entry yet
    string?    TodayNotes,
    double?    YesterdayWeightKg,    // null → no entry
    double?    TodayIdealWeightKg,   // null → no ideal row for today
    double?    WeekFirstWeightKg,    // weight of first entry in current ISO week
    int        WeekDaysLogged,       // count of entries in current ISO week
    double?    IdealWeeklyLossKg,    // null → profile incomplete
    UnitSystem UnitSystem            // for display conversion in ViewModel
);
```

---

### Step 3 — Application: `GetLogContextQuery`

**Files:**
- `src/LeanAI.Application/WeightManagement/Queries/GetLogContext/GetLogContextQuery.cs`
- `src/LeanAI.Application/WeightManagement/Queries/GetLogContext/GetLogContextQueryHandler.cs`

```csharp
public record GetLogContextQuery(DateOnly Date) : IRequest<LogContextDto>;
```

**Handler logic:**
1. Load `UserProfile` via `IUserProfileRepository`.
2. Load today's entry: `IDailyActualWeightRepository.GetByDateAsync(Date)`.
3. Load yesterday's entry: `GetByDateAsync(Date.AddDays(-1))`.
4. Compute ISO week boundaries:
   ```csharp
   var daysSinceMonday = ((int)Date.DayOfWeek + 6) % 7;
   var weekStart = Date.AddDays(-daysSinceMonday);
   var weekEnd   = weekStart.AddDays(6);
   ```
5. Load week entries: `GetRangeAsync(weekStart, weekEnd)`.
6. Load today's ideal: `IDailyIdealWeightRepository.GetByDateAsync(Date)`.
7. Compute `IdealWeeklyLossKg`:
   ```csharp
   double? idealWeekly = null;
   if (profile?.StartingWeightKg.HasValue == true && profile.TargetWeightKg.HasValue && profile.TargetPeriod.HasValue)
       idealWeekly = (profile.StartingWeightKg.Value - profile.TargetWeightKg.Value) / profile.TargetPeriod.Value.TotalDays() * 7;
   ```
8. Return `LogContextDto`.

---

### Step 4 — Application: `UpsertDailyLogCommand`

**Files:**
- `src/LeanAI.Application/WeightManagement/Commands/UpsertDailyLog/UpsertDailyLogCommand.cs`
- `src/LeanAI.Application/WeightManagement/Commands/UpsertDailyLog/UpsertDailyLogCommandHandler.cs`

```csharp
public record UpsertDailyLogCommand(DateOnly Date, double WeightKg, string? Notes) : IRequest;
```

**Handler logic:**
1. `var existing = await repo.GetByDateAsync(command.Date)`.
2. If `null` → create new `DailyActualWeight { Date = ..., WeightKg = ..., Notes = ... }`.
3. Else → set `existing.WeightKg = command.WeightKg; existing.Notes = command.Notes`.
4. Call `repo.UpsertAsync(entity)`.

---

### Step 5 — Application Tests (TDD — write FIRST, run RED, then implement handlers)

**File:** `tests/LeanAI.Application.Tests/WeightManagement/Queries/GetLogContextQueryHandlerTests.cs`

Test cases:
- `Returns_TodayWeightKg_and_Notes_when_entry_exists`
- `Returns_null_TodayWeightKg_when_no_entry`
- `Returns_YesterdayWeightKg_when_yesterday_entry_exists`
- `Returns_null_YesterdayWeightKg_when_no_yesterday_entry`
- `Returns_TodayIdealWeightKg_when_ideal_exists`
- `Returns_null_TodayIdealWeightKg_when_no_ideal_row`
- `Returns_correct_WeekFirstWeightKg_and_WeekDaysLogged`
- `Returns_null_WeekFirstWeightKg_and_zero_days_when_no_week_entries`
- `Returns_IdealWeeklyLossKg_from_complete_profile`
- `Returns_null_IdealWeeklyLossKg_when_profile_missing`
- `Uses_UnitSystem_from_profile`

**File:** `tests/LeanAI.Application.Tests/WeightManagement/Commands/UpsertDailyLogCommandHandlerTests.cs`

Test cases:
- `Creates_new_entry_when_none_exists` (GetByDateAsync returns null → Upsert called with new entity)
- `Updates_existing_entry_when_one_exists` (GetByDateAsync returns entity → Upsert called with updated entity)

---

### Step 6 — Infrastructure: Repositories

**New file:** `src/LeanAI.Infrastructure/Repositories/DailyActualWeightRepository.cs`

```csharp
public sealed class DailyActualWeightRepository : IDailyActualWeightRepository
{
    // GetByDateAsync: context.DailyActualWeights.FirstOrDefaultAsync(x => x.Date == date, ct)
    // GetRangeAsync:  context.DailyActualWeights.Where(x => x.Date >= from && x.Date <= to).ToListAsync(ct)
    // UpsertAsync:    if (entry.Id == Guid.Empty) { entry.Id = Guid.NewGuid(); context.Add(entry); }
    //                 else { context.Update(entry); }
    //                 await context.SaveChangesAsync(ct);
}
```

**Extend** `DailyIdealWeightRepository` with `GetByDateAsync`.

**Register** in `src/LeanAI.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<IDailyActualWeightRepository, DailyActualWeightRepository>();
```

---

### Step 7 — No EF Migration Required

`DailyActualWeights` and `DailyIdealWeights` tables are already created in migration `20260510204522_Add_DailyIdealWeights_DailyActualWeights`. No new migration needed for Phase 4.

---

### Step 8 — Presentation: `LogViewModel`

**File:** `src/LeanAI.Maui/ViewModels/LogViewModel.cs`

**Observable properties:**
| Property | Type | Purpose |
|---|---|---|
| `EntryDate` | `DateOnly` | Always `DateOnly.FromDateTime(DateTime.Today)` |
| `WeightDisplayText` | `string` | User-typed weight in display units |
| `Notes` | `string?` | Optional comments |
| `UnitLabel` | `string` | `"kg"` or `"lb"` |
| `IsAutosaving` | `bool` | Drives spinner visibility |
| `YesterdayDeltaText` | `string` | `"+0.4 kg"`, `"−0.4 kg"`, or `"—"` |
| `IsDeltaGain` | `bool` | `true` → Nickel, `false` → Copper (via converter) |
| `HasAnalysis` | `bool` | `false` when context is not yet loaded |
| `IsAboveIdeal` | `bool` | Drives warning box visibility |
| `AboveIdealDeltaText` | `string` | `"+0.3 kg"` |
| `CurrentWeeklyLossText` | `string` | `"1.1 kg"` or `"—"` |
| `IdealWeeklyLossText` | `string` | `"0.9 kg"` or `"—"` |

**Commands:**
- `[RelayCommand] Task LoadLogAsync()` — calls `GetLogContextQuery(today)`, populates backing context fields, pre-fills `WeightDisplayText` and `Notes`, calls `RecalculateIndicators()`.
- `[RelayCommand] Task SaveLogAsync()` — called by debounce, calls `UpsertDailyLogCommand`. Sets `IsAutosaving = true` before, `false` after.

**Debounce pattern:**
```csharp
private CancellationTokenSource? _saveCts;

partial void OnWeightDisplayTextChanged(string value)
{
    RecalculateIndicators();
    TriggerSave();
}

partial void OnNotesChanged(string? value) => TriggerSave();

private void TriggerSave()
{
    _saveCts?.Cancel();
    _saveCts = new CancellationTokenSource();
    _ = SaveWithDebounceAsync(_saveCts.Token);
}

private async Task SaveWithDebounceAsync(CancellationToken ct)
{
    try
    {
        await Task.Delay(500, ct);
        IsAutosaving = true;
        var weightKg = ParseToKg(WeightDisplayText);
        if (weightKg.HasValue)
            await _mediator.Send(new UpsertDailyLogCommand(EntryDate, weightKg.Value, Notes), ct);
        IsAutosaving = false;
    }
    catch (OperationCanceledException) { }
}
```

**`RecalculateIndicators()`:**
- Parse `WeightDisplayText` → kg.
- Compute yesterday delta.
- Check vs. today's ideal.
- Compute projected weekly loss.
- Update all display properties.

---

### Step 9 — Presentation: `LogPage.xaml`

**File:** `src/LeanAI.Maui/Views/Log/LogPage.xaml`

Full layout matching the mockup:

```
ScrollView
└─ VerticalStackLayout (Padding="24,32" Spacing="24")
   ├─ Header label: "Daily Entry | {EntryDate:dd MMM yyyy}"
   │
   ├─ Border (weight input card, ColorBase background, CornerRadius=12)
   │  └─ VerticalStackLayout
   │     ├─ Label "Current Weight ({UnitLabel})" — Nickel, 13px
   │     ├─ Entry WeightDisplayText — large font (40px+), Copper border, Keyboard=Numeric
   │     └─ Label "{UnitLabel}" — Nickel, 13px, centered
   │
   ├─ Border (comments card)
   │  └─ VerticalStackLayout
   │     ├─ Label "Comments (Optional)" — Nickel, 13px
   │     └─ Editor Notes — multi-line, 15px, Nickel placeholder
   │
   ├─ Label "Real-time Analysis" — Nickel, 11px, CharacterSpacing=2
   │
   ├─ Grid (ColumnDefinitions="*,12,*")  [two-column analysis cards]
   │  ├─ Border (Yesterday Delta card) [Col 0]
   │  │  └─ VerticalStackLayout
   │  │     ├─ Label "Yesterday Delta" — Nickel, 11px
   │  │     └─ Label YesterdayDeltaText — large, color via BoolToColorConverter(IsDeltaGain)
   │  └─ Border (Weekly Prediction card) [Col 2]
   │     └─ VerticalStackLayout
   │        ├─ Label "Predicted Weekly Loss" — Nickel, 11px
   │        ├─ Grid
   │        │  ├─ Label "Current" — Nickel, 11px
   │        │  ├─ Label CurrentWeeklyLossText — 15px, Copper or Nickel
   │        │  ├─ Label "Ideal" — Nickel, 11px
   │        │  └─ Label IdealWeeklyLossText — 15px, Nickel
   │
   ├─ Border (Warning box, IsVisible="{Binding IsAboveIdeal}")
   │  BackgroundColor=Copper, CornerRadius=10
   │  └─ Grid
   │     ├─ Label "⚠" icon
   │     └─ VerticalStackLayout
   │        ├─ Label "Warning: Weight > Ideal" — Bold, ColorBase
   │        └─ Label "You are {AboveIdealDeltaText} above today's ideal line" — ColorBase
   │
   └─ VerticalStackLayout (autosave footer, IsVisible="{Binding IsAutosaving}")
      ├─ ActivityIndicator IsRunning="{Binding IsAutosaving}" Color=Copper
      ├─ Label "Autosaving..." — Nickel, 13px, centered
      └─ Label "Data is debounced and saved automatically" — Nickel, 11px, centered
```

**LogPage.xaml.cs:** `OnAppearing` calls `ViewModel.LoadLogCommand.Execute(null)`.

---

### Step 10 — App Startup: Autoload Behavior

**File:** `src/LeanAI.Maui/App.xaml.cs`

In the existing `ProvisionAiSettingsAsync` (or a new `ProvisionAppAsync`), after all loading:

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
var logContext = await mediator.Send(new GetLogContextQuery(today));
if (logContext.TodayWeightKg == null)
    await Shell.Current.GoToAsync("//log");
```

This ensures: if the user closed the app on the Trends tab yesterday and relaunches today with no entry, they land on the Log screen.

---

### Step 11 — DI Registration

Confirm in `src/LeanAI.Maui/MauiProgram.cs`:
- `LogPage` registered (likely already there as a stub)
- `LogViewModel` registered as Transient

---

### Step 12 — Final Validation

```
dotnet test   → all tests green (100% branch coverage on handler tests)
dotnet build  → 0 errors, 0 warnings
```

---

## Non-Goals for Phase 4
- Editing **past** dates (read-only history is Phase 5)
- Calendar / Evolution / Trends view (Phase 5)
- Chart rendering (Phase 5)

---

## TDD Order of Work
1. **Domain** (Step 1) — interface definitions only, no logic.
2. **Application DTOs** (Step 2).
3. **Write tests** (Step 5) → run → RED.
4. **Implement handlers** (Steps 3–4) → run → GREEN.
5. **Infrastructure** (Step 6) — repositories and DI registration.
6. **Presentation** (Steps 8–9) — ViewModel + XAML.
7. **App startup** (Step 10).
8. **Build & test** (Step 12).

---

## Open Questions (Resolved)
| Question | Answer |
|---|---|
| Color semantics on Log screen | Follow global spec: Copper = good/on-track, Nickel = warning/off-track |
| Log tab when today's entry exists | Same entry screen, pre-filled for editing |
| Yesterday Delta when no prior entry | Show `—` (dash) |
| Weekly prediction formula | `(firstWeekWeight − currentWeight) / daysLogged × 7` |

---

## Release Notes — 2026-05-11

### What shipped
- **Log tab — Daily Entry screen** replacing the empty stub. The full weight entry UI is live on Android.
- **Autosave**: Weight and comments are debounced (500 ms) and saved to SQLite automatically. No Save button. "Autosaving…" spinner appears during the write.
- **Yesterday Delta**: Shows the difference vs. the previous logged entry (+/− in display units). Copper = lost weight (on-track), Nickel = gained weight. Displays `—` on the first day of tracking.
- **Ideal Warning**: A full-width Copper warning box appears whenever current weight exceeds today's ideal line, showing exactly how many units above the ideal the user is.
- **Predicted Weekly Loss**: Two-column card showing projected weekly loss (current week trend extrapolated to 7 days) vs. the ideal weekly loss derived from the user's goal. Displays `—` until at least 2 entries exist in the current ISO week.
- **Unit-aware**: All display converts automatically between kg and lb based on the user profile preference set during setup. All DB writes remain in kg.
- **Autoload**: App launch checks whether today's entry exists; if not, the Shell navigates to the Log tab automatically so the user lands directly on the entry screen.

### Files added
| File | Purpose |
|---|---|
| `src/LeanAI.Domain/WeightManagement/Interfaces/IDailyActualWeightRepository.cs` | Repository contract |
| `src/LeanAI.Application/WeightManagement/DTOs/LogContextDto.cs` | Context record for the Log screen |
| `src/LeanAI.Application/WeightManagement/Queries/GetLogContext/` | Query + handler |
| `src/LeanAI.Application/WeightManagement/Commands/UpsertDailyLog/` | Command + handler |
| `src/LeanAI.Infrastructure/Repositories/DailyActualWeightRepository.cs` | EF Core implementation |
| `src/LeanAI.Maui/ViewModels/LogViewModel.cs` | Full ViewModel with debounce and reactive indicators |
| `src/LeanAI.Maui/Views/Log/LogPage.xaml` | Complete UI (replaced stub) |
| `tests/LeanAI.Tests/Application/WeightManagement/GetLogContextQueryHandlerTests.cs` | 11 tests |
| `tests/LeanAI.Tests/Application/WeightManagement/UpsertDailyLogCommandHandlerTests.cs` | 2 tests |

### Files modified
| File | Change |
|---|---|
| `src/LeanAI.Domain/WeightManagement/Interfaces/IDailyIdealWeightRepository.cs` | Added `GetByDateAsync` |
| `src/LeanAI.Infrastructure/Repositories/DailyIdealWeightRepository.cs` | Implemented `GetByDateAsync` |
| `src/LeanAI.Infrastructure/DependencyInjection.cs` | Registered `IDailyActualWeightRepository` |
| `src/LeanAI.Maui/Views/Log/LogPage.xaml.cs` | Wired `LogViewModel`, `OnAppearing` load |
| `src/LeanAI.Maui/MauiProgram.cs` | Registered `LogViewModel` |
| `src/LeanAI.Maui/App.xaml.cs` | Added `NavigateToLogIfNoEntryTodayAsync` |

### Bug fixed during verification
- `NavigateToLogIfNoEntryTodayAsync` used `"//log"` (lowercase); MAUI Shell routes are case-sensitive and the registered route is `"Log"`. Fixed to `"//Log"`.

### Test results
- **Before Phase 4:** 50 tests passing
- **After Phase 4:** 64 tests passing (14 new), 0 failures, 0 skipped
- `dotnet build` → 0 errors, 0 warnings
