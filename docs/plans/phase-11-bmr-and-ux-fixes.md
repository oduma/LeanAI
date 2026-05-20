# Phase 11: BMR, Activity Notes Removal & Calorie Tile Polishing

## Status: DONE ✅

---

## Clarifications confirmed

| Question | Answer |
|----------|--------|
| BMR role in net calculation | Subtracted like activity — net = Food − Activity − BMR |
| BMR storage | New `CaloryLog` row with `SourceType = "bmr"` per day |
| BMR trigger | Recalculated on every weight save/update (upsert semantics) |
| Tile sign format | Always signed: "+1 847 kcal" (nickel) when > 0, "−300 kcal" (copper) when ≤ 0; "—" (nickel) only when no data |
| BMR formula | Mifflin-St Jeor (local computation — no AI call) |

---

## Scope

1. **BMR setting** — new "Use BMR" toggle in General Settings.
2. **BMR calculation** — computed locally with the Mifflin-St Jeor formula after each weight save; result stored as `CaloryLog { SourceType = "bmr" }`.
3. **BMR in Calories Detail** — separate non-editable section at the top of the detail screen.
4. **Net calculation** — `GetTotalCaloriesAsync` returns `double?` (null = no data); net = food − activity − bmr.
5. **Activity notes removal** — `SaveRunActivitiesCommandHandler` no longer calls `AppendActivityCommentCommand`.
6. **Tile display** — signed value with inverted color rule (copper ≤ 0, nickel > 0).

---

## BMR formula (Mifflin-St Jeor)

```
Male:   BMR = (10 × weight_kg) + (6.25 × height_cm) − (5 × age) − 5
Female: BMR = (10 × weight_kg) + (6.25 × height_cm) − (5 × age) − 161
```

This is a pure arithmetic operation — no network call, no async, no failure path. It is computed inline inside `CalculateAndSaveBmrCommandHandler`.

---

## Architecture decisions

| Decision | Rationale |
|----------|-----------|
| `CaloryLog { SourceType = "bmr" }` for storage | Reuses existing ledger table; net query adds "bmr" to the subtracted types alongside "activity" |
| BMR computed locally with Mifflin-St Jeor formula | Deterministic, instant, no network dependency — no Gemini call, no `IBmrCalculationService` interface needed |
| `CalculateAndSaveBmrCommand` dispatched from `LogViewModel` | Keeps weight-save SRP intact; VM decides when to trigger the recalculation |
| Track `_lastSavedWeightKg` in VM | Avoid redundant BMR upserts on notes-only auto-saves |
| `GetTotalCaloriesAsync` returns `double?` | Distinguishes "no data" (null → "—") from "balanced net = 0" (0.0 → "0 kcal" copper) |
| Remove `AppendActivityCommentCommand` from `SaveRunActivitiesCommandHandler` | Activities are viewable in Calories Detail; duplicating them in free-text notes adds noise |

---

## Step-by-step implementation plan

---

### Step 1 — Domain: Add `UseBmr` to `AppSettings`

**File to modify:** `src/LeanAI.Domain/WeightManagement/Entities/AppSettings.cs`

Add:
```csharp
public bool UseBmr { get; set; }
```

---

### Step 2 — Domain: Update `ICaloryLogRepository`

**File to modify:** `src/LeanAI.Domain/FoodTracking/Interfaces/ICaloryLogRepository.cs`

- Change `GetTotalCaloriesAsync` return type: `Task<double>` → `Task<double?>`.
  - `null` = no food, no activity, no BMR data for the date.
  - Net is computed as `food − activity − bmr`.
- Add:
  ```csharp
  Task<CaloryLog?> GetBmrForDateAsync(DateOnly date, CancellationToken ct = default);
  Task UpsertBmrAsync(DateOnly date, double calories, CancellationToken ct = default);
  ```

---

### Step 3 — Application: Update `AppSettingsDto`, `SaveAppSettingsCommand`, `GetAppSettingsQuery`

**Files to modify:**

- `AppSettingsDto`: add `bool UseBmr` to the record.
- `SaveAppSettingsCommand`: add `bool UseBmr` parameter.
- `SaveAppSettingsCommandHandler`: map `UseBmr` onto the `AppSettings` entity before saving.
- `GetAppSettingsQueryHandler`: include `UseBmr` in the returned `AppSettingsDto`.

---

### Step 4 — Application: New `CalculateAndSaveBmrCommand`

**Files to create:**
```
src/LeanAI.Application/WeightManagement/Commands/CalculateAndSaveBmr/CalculateAndSaveBmrCommand.cs
src/LeanAI.Application/WeightManagement/Commands/CalculateAndSaveBmr/CalculateAndSaveBmrCommandHandler.cs
```

**Command:**
```csharp
public sealed record CalculateAndSaveBmrCommand(DateOnly Date, double WeightKg) : IRequest;
```

**Handler — injects:** `IAppSettingsRepository`, `IUserProfileRepository`, `ICaloryLogRepository`

**Handler logic:**
1. Load `AppSettings` → if `!UseBmr`, return immediately (no-op).
2. Load `UserProfile` → if `HeightCm`, `Gender`, or `Age` is null, return (insufficient data).
3. Compute BMR using the Mifflin-St Jeor formula:
   ```csharp
   double bmr = (10 * WeightKg) + (6.25 * heightCm) - (5 * age)
                + (gender == Gender.Male ? -5 : -161);
   ```
4. Call `ICaloryLogRepository.UpsertBmrAsync(Date, bmr)`.

---

### Step 5 — Application: New `GetBmrForDateQuery`

**Files to create:**
```
src/LeanAI.Application/WeightManagement/Queries/GetBmrForDate/GetBmrForDateQuery.cs
src/LeanAI.Application/WeightManagement/Queries/GetBmrForDate/GetBmrForDateQueryHandler.cs
```

**Query:** `record(DateOnly Date) : IRequest<double?>`

**Handler:** calls `ICaloryLogRepository.GetBmrForDateAsync(request.Date)` and returns `caloryLog?.Calories`.

---

### Step 6 — Application: Update `GetTotalCaloriesForDateQuery`

**Files to modify:**

- `GetTotalCaloriesForDateQuery.cs`: `IRequest<double>` → `IRequest<double?>`.
- `GetTotalCaloriesForDateQueryHandler.cs`: handler return type `double` → `double?` to match the updated repository.

---

### Step 7 — Application: Update `SaveRunActivitiesCommandHandler`

**File to modify:**
`src/LeanAI.Application/ActivityTracking/Commands/SaveRunActivities/SaveRunActivitiesCommandHandler.cs`

Remove the block that calls `AppendActivityCommentCommand` (both the `allTexts` collection and the `mediator.Send` call at the end). Also remove the `IMediator` constructor parameter and using directive if it becomes unused.

---

### Step 8 — Infrastructure: Update `CaloryLogRepository`

**File to modify:**
`src/LeanAI.Infrastructure/FoodTracking/Repositories/CaloryLogRepository.cs`

1. **`GetTotalCaloriesAsync`** returns `double?`:
   ```csharp
   var hasFoodData   = await context.FoodLogs.AnyAsync(fl => fl.Date == date, ct);
   var hasCaloryData = await context.CaloryLogs.AnyAsync(cl => cl.Date == date, ct);
   if (!hasFoodData && !hasCaloryData) return null;

   var food   = await context.FoodLogs
                             .Where(fl => fl.Date == date)
                             .SumAsync(fl => fl.CaloryLog.Calories, ct);
   var burned = await context.CaloryLogs
                             .Where(cl => cl.Date == date
                                       && (cl.SourceType == "activity" || cl.SourceType == "bmr"))
                             .SumAsync(cl => cl.Calories, ct);
   return food - burned;
   ```

2. **`GetBmrForDateAsync`**:
   ```csharp
   return await context.CaloryLogs
                       .Where(cl => cl.Date == date && cl.SourceType == "bmr")
                       .FirstOrDefaultAsync(ct);
   ```

3. **`UpsertBmrAsync`**:
   ```csharp
   var existing = await context.CaloryLogs
                               .Where(cl => cl.Date == date && cl.SourceType == "bmr")
                               .FirstOrDefaultAsync(ct);
   if (existing is not null) context.CaloryLogs.Remove(existing);
   context.CaloryLogs.Add(new CaloryLog { Date = date, Calories = calories, SourceType = "bmr" });
   await context.SaveChangesAsync(ct);
   ```

---

### Step 9 — Infrastructure: EF Core — update `AppSettings` model + migration

**File to modify:** `src/LeanAI.Infrastructure/Persistence/LeanAIDbContext.cs`

In the `AppSettings` entity configuration block, add:
```csharp
entity.Property(e => e.UseBmr).IsRequired();
```

**Migration to run:**
```
dotnet ef migrations add Add_UseBmr_To_AppSettings --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Maui
```
Adds `UseBmr INTEGER NOT NULL DEFAULT 0` to `AppSettings`.

---

### Step 10 — Presentation: `SettingsViewModel`

**File to modify:** `src/LeanAI.Maui/ViewModels/SettingsViewModel.cs`

1. Add `[ObservableProperty] private bool _useBmr;`
2. Add partial handler (immediate save on toggle, matching `CalendarFirstDay` pattern):
   ```csharp
   partial void OnUseBmrChanged(bool value)
   {
       if (_isLoading) return;
       _ = SaveImmediateAsync();
   }
   ```
3. In `LoadAiSettingsAsync`: `UseBmr = dto.UseBmr;`
4. In `SaveImmediateAsync`: pass `UseBmr` to `SaveAppSettingsCommand`.

---

### Step 11 — Presentation: `SettingsPage.xaml`

**File to modify:** `src/LeanAI.Maui/Views/Settings/SettingsPage.xaml`

Add to the **GENERIC SETTINGS** section, below the First Day of Week picker:

```xml
<Grid ColumnDefinitions="*,Auto" Padding="14,12">
    <VerticalStackLayout Grid.Column="0" Spacing="2">
        <Label Text="Use BMR (Basal Metabolic Rate)"
               TextColor="{StaticResource ColorLight}"
               FontSize="15" />
        <Label Text="Subtracts your estimated resting calorie burn from the daily net"
               TextColor="{StaticResource ColorNickel}"
               FontSize="12" />
    </VerticalStackLayout>
    <Switch Grid.Column="1"
            IsToggled="{Binding UseBmr}"
            OnColor="{StaticResource ColorCopper}"
            VerticalOptions="Center" />
</Grid>
```

---

### Step 12 — Presentation: `LogViewModel`

**File to modify:** `src/LeanAI.Maui/ViewModels/LogViewModel.cs`

**A. Track weight changes to avoid redundant BMR upserts:**

Add a field:
```csharp
private double? _lastSavedWeightKg;
```

**B. Dispatch BMR command only when weight actually changed:**

In `SaveWithDebounceAsync`, after the `UpsertDailyLogCommand` send:
```csharp
await _mediator.Send(new UpsertDailyLogCommand(EntryDate, weightKg.Value, Notes), ct);

if (weightKg != _lastSavedWeightKg)
{
    _lastSavedWeightKg = weightKg;
    await _mediator.Send(new CalculateAndSaveBmrCommand(EntryDate, weightKg.Value), ct);
}
```

No try-catch needed — BMR is pure arithmetic and the handler is a no-op when `UseBmr = false`.

**C. Update calorie tile properties:**

Replace the existing two-line tile update with:
```csharp
var totalCal = await _mediator.Send(new GetTotalCaloriesForDateQuery(date), ct);
HasCalories       = totalCal.HasValue;
IsCaloriesDeficit = totalCal.HasValue && totalCal.Value <= 0;
TotalCaloriesText = totalCal switch
{
    null => "—",
    > 0  => $"+{totalCal.Value:N0} kcal",
    _    => $"{totalCal.Value:N0} kcal"   // 0 → "0 kcal", negative → "-300 kcal"
};
```

Add the new observable property declaration alongside the other `[ObservableProperty]` fields:
```csharp
[ObservableProperty] private bool _isCaloriesDeficit;
```

---

### Step 13 — Presentation: `LogPage.xaml`

**File to modify:** `src/LeanAI.Maui/Views/Log/LogPage.xaml`

Change the Calories tile value label color binding:
```xml
<!-- Before -->
TextColor="{Binding HasCalories, Converter={StaticResource BoolToColorConverter}, ConverterParameter='ColorCopper|ColorNickel'}"

<!-- After -->
TextColor="{Binding IsCaloriesDeficit, Converter={StaticResource BoolToColorConverter}, ConverterParameter='ColorCopper|ColorNickel'}"
```

---

### Step 14 — Presentation: `CaloriesDetailViewModel`

**File to modify:** `src/LeanAI.Maui/ViewModels/CaloriesDetailViewModel.cs`

1. Add observable properties:
   ```csharp
   [ObservableProperty] private string _bmrText = "—";
   [ObservableProperty] private bool   _hasBmr;
   ```

2. In `LoadAsync`, after loading food and activity:
   ```csharp
   var bmr = await mediator.Send(new GetBmrForDateQuery(_date));
   HasBmr  = bmr.HasValue;
   BmrText = bmr.HasValue ? $"−{bmr.Value:N0} kcal" : "—";

   var foodTotal     = FoodItems.Sum(f => f.Calories);
   var activityTotal = ActivityItems.Sum(a => a.Calories);
   var bmrTotal      = bmr ?? 0;
   var net           = foodTotal - activityTotal - bmrTotal;

   FoodTotalText     = foodTotal     > 0 ? $"{foodTotal:N0} kcal"     : "—";
   ActivityTotalText = activityTotal > 0 ? $"{activityTotal:N0} kcal" : "—";
   NetCaloriesText   = !bmr.HasValue && foodTotal == 0 && activityTotal == 0
                       ? "—"
                       : $"{net:N0} kcal";
   ```

---

### Step 15 — Presentation: `CaloriesDetailPage.xaml`

**File to modify:** `src/LeanAI.Maui/Views/CaloriesDetail/CaloriesDetailPage.xaml`

Add a **BMR section** at the very top of the content area (before the Food section). Only visible when `HasBmr` is true.

```xml
<!-- BMR Section (formula-calculated, read-only) -->
<Border BackgroundColor="#2A2A2A" StrokeThickness="0" IsVisible="{Binding HasBmr}">
    <Border.StrokeShape><RoundRectangle CornerRadius="10" /></Border.StrokeShape>
    <VerticalStackLayout Padding="14,14" Spacing="4">
        <Label Text="Basal Metabolic Rate"
               TextColor="{StaticResource ColorNickel}"
               FontSize="13" CharacterSpacing="1" />
        <Label Text="{Binding BmrText}"
               TextColor="{StaticResource ColorCopper}"
               FontSize="26" FontAttributes="Bold" />
        <Label Text="Mifflin-St Jeor · Read only"
               TextColor="{StaticResource ColorNickel}"
               FontSize="11" />
    </VerticalStackLayout>
</Border>
```

---

### Step 16 — Tests

**New test files:**

1. `tests/LeanAI.Tests/Application/WeightManagement/Commands/CalculateAndSaveBmrCommandHandlerTests.cs`
   - `Handle_UseBmrFalse_IsNoOp` — `UpsertBmrAsync` never called
   - `Handle_ProfileMissingHeight_IsNoOp`
   - `Handle_ProfileMissingGender_IsNoOp`
   - `Handle_ProfileMissingAge_IsNoOp`
   - `Handle_Male_ComputesCorrectBmrAndUpserts` — verify formula: (10×weight)+(6.25×height)−(5×age)−5
   - `Handle_Female_ComputesCorrectBmrAndUpserts` — verify formula: (10×weight)+(6.25×height)−(5×age)−161

2. `tests/LeanAI.Tests/Application/WeightManagement/Queries/GetBmrForDateQueryHandlerTests.cs`
   - `Handle_BmrExists_ReturnsCalories`
   - `Handle_NoBmr_ReturnsNull`

**Updated test files:**

3. `tests/LeanAI.Tests/Application/FoodTracking/Queries/GetTotalCaloriesForDateQueryHandlerTests.cs`
   - Return type expectations updated to `double?`
   - Add `Handle_NoData_ReturnsNull`
   - Add `Handle_FoodPlusActivityPlusBmr_ReturnsCorrectNet`

4. `tests/LeanAI.Tests/Application/ActivityTracking/Commands/SaveRunActivitiesCommandHandlerTests.cs`
   - All tests: verify `AppendActivityCommentCommand` is NEVER called (import and edit mode).

5. `tests/LeanAI.Tests/Application/WeightManagement/Queries/GetAppSettingsQueryHandlerTests.cs` *(if exists)*
   - Add `UseBmr` field assertions.

6. `tests/LeanAI.Tests/Application/WeightManagement/Commands/SaveAppSettingsCommandHandlerTests.cs` *(if exists)*
   - Add `UseBmr` field assertions.

---

## File change summary

| File | Action |
|------|--------|
| `Domain/WeightManagement/Entities/AppSettings.cs` | MODIFY — add `UseBmr` |
| `Domain/FoodTracking/Interfaces/ICaloryLogRepository.cs` | MODIFY — `double?` return; 2 new methods |
| `Application/WeightManagement/DTOs/AppSettingsDto.cs` | MODIFY — add `UseBmr` |
| `Application/WeightManagement/Commands/SaveAppSettings/SaveAppSettingsCommand.cs` | MODIFY — add `UseBmr` |
| `Application/WeightManagement/Commands/SaveAppSettings/SaveAppSettingsCommandHandler.cs` | MODIFY — map `UseBmr` |
| `Application/WeightManagement/Queries/GetAppSettings/GetAppSettingsQueryHandler.cs` | MODIFY — include `UseBmr` |
| `Application/WeightManagement/Commands/CalculateAndSaveBmr/CalculateAndSaveBmrCommand.cs` | CREATE |
| `Application/WeightManagement/Commands/CalculateAndSaveBmr/CalculateAndSaveBmrCommandHandler.cs` | CREATE |
| `Application/WeightManagement/Queries/GetBmrForDate/GetBmrForDateQuery.cs` | CREATE |
| `Application/WeightManagement/Queries/GetBmrForDate/GetBmrForDateQueryHandler.cs` | CREATE |
| `Application/FoodTracking/Queries/GetTotalCaloriesForDate/GetTotalCaloriesForDateQuery.cs` | MODIFY — `IRequest<double?>` |
| `Application/FoodTracking/Queries/GetTotalCaloriesForDate/GetTotalCaloriesForDateQueryHandler.cs` | MODIFY — `double?` return |
| `Application/ActivityTracking/Commands/SaveRunActivities/SaveRunActivitiesCommandHandler.cs` | MODIFY — remove `AppendActivityCommentCommand` call |
| `Infrastructure/FoodTracking/Repositories/CaloryLogRepository.cs` | MODIFY — `double?`; bmr in net; 2 new methods |
| `Infrastructure/Persistence/LeanAIDbContext.cs` | MODIFY — `UseBmr` config for `AppSettings` |
| `Infrastructure/Migrations/…_Add_UseBmr_To_AppSettings` | GENERATE |
| `Maui/ViewModels/SettingsViewModel.cs` | MODIFY — `UseBmr` observable + auto-save |
| `Maui/Views/Settings/SettingsPage.xaml` | MODIFY — add BMR toggle row |
| `Maui/ViewModels/LogViewModel.cs` | MODIFY — BMR dispatch + signed tile text + `IsCaloriesDeficit` |
| `Maui/Views/Log/LogPage.xaml` | MODIFY — tile color binding → `IsCaloriesDeficit` |
| `Maui/ViewModels/CaloriesDetailViewModel.cs` | MODIFY — BMR section + updated net calc |
| `Maui/Views/CaloriesDetail/CaloriesDetailPage.xaml` | MODIFY — add BMR section at top |
| Tests — 2 new files + 4 updated files | CREATE / MODIFY |

---

## Definition of Done

- ✅ "Use BMR" toggle visible in General Settings; saves immediately on toggle
- ✅ Saving a weight entry (when BMR is enabled) computes BMR via Mifflin-St Jeor and stores a `CaloryLog { SourceType = "bmr" }` row for the date
- ✅ Editing weight (same day) recomputes and upserts BMR; notes-only saves do not trigger recomputation
- ✅ Male formula: `(10×w)+(6.25×h)−(5×age)−5`; female: `(10×w)+(6.25×h)−(5×age)−161`
- ✅ BMR section visible at the top of Calories Detail screen when a BMR entry exists; shows "−N kcal" in Copper; subtitle "Mifflin-St Jeor · Read only"; no Edit button
- ✅ Net calories tile and detail screen both compute: food − activity − bmr; tile refreshes immediately on weight save
- ✅ Tile shows "—" (nickel) when no food / activity / BMR data exists for the day
- ✅ Tile shows signed value: "+N kcal" (nickel) when net > 0; "−N kcal" or "0 kcal" (copper) when net ≤ 0
- ✅ Activities are no longer appended to `DailyActualWeight.Notes`
- ✅ Notes field in daily log remains fully user-editable
- ✅ `dotnet build` → 0 errors, pre-existing warnings only
- ✅ 190 / 190 tests passing
- ✅ `FUNCTIONAL_REQUIREMENTS.md` updated
