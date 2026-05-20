# Phase 10: Import Runs Improvements ✅ COMPLETE

## Status: COMPLETE (shipped 2026-05-18)

---

## Summary

Enhance the run-import flow with a review screen (mirroring the food-review screen), activity calorie tracking, and a new Calories Detail screen accessible from the Daily Log tile.

**Scope:**
1. Gemini run-image analysis now also returns `calories_burned`
2. New **Run Review Screen** — date picker, editable activity rows, read-only calories column, re-evaluate, total, save
3. Activity calories persisted to `CaloryLog` (`SourceType = "activity"`)
4. New `CustomActivityLog` table for manually-added activity rows
5. Net calories on Daily Log tile (food – activity)
6. New **Calories Detail Screen** — three sections: food, activity, grand total
7. Calories tile navigates to detail screen (replaces direct link to food review)

---

## Clarifications confirmed

| Question | Answer |
|----------|--------|
| Row structure | One row per imported run (full commentary text in col 1, calories in col 2); user can add extra rows manually |
| Calories column | Read-only; updated only via Re-evaluate |
| Re-evaluate input | Text descriptions only (no image) |
| Weight log notes | Commentary still appended to `DailyActualWeight.Notes` as before |
| Manual row calories | Start empty; must hit Re-evaluate for AI estimate |
| Calorie sign in DB | Positive with `SourceType = "activity"`; net = food − activity |
| Calories tile | Already navigates (currently to FoodReviewPage) → will change to CaloriesDetailPage |

---

## Architecture overview

### New import flow

```
Share sheet
  └── ImportRunActivity
        ├── Call AnalyzeRunImageCommand (Gemini analysis only — no persistence)
        ├── Store RunActivityRowDto in RunImportStateService
        ├── Launch MainActivity (NewTask | SingleTop)
        └── Finish

MainActivity/LogViewModel.OnAppearing
  └── RunImportStateService.HasPending?
        └── PushModalAsync(RunReviewPage, isImportMode=true)

RunReviewPage [Save]
  └── SaveRunActivitiesCommand
        ├── For run rows: ActivityLog metrics + Notes comment + CaloryLog (activity)
        └── For custom rows: CaloryLog (activity) + CustomActivityLog
```

### Calories Detail flow

```
Calories tile (LogPage)
  └── OpenCaloriesDetailCommand
        └── PushModalAsync(CaloriesDetailPage)
              ├── Section 1: Food items (from GetFoodLogForDateQuery)
              ├── Section 2: Activity items (from GetActivityCaloriesForDateQuery)
              └── Grand Total (net = food – activity)
              ├── [Edit Food] → PushModalAsync(FoodReviewPage)
              └── [Edit Activities] → PushModalAsync(RunReviewPage, isImportMode=false)
```

---

## Step-by-step implementation plan

---

### Step 1 — Domain: new entity `CustomActivityLog`

**File:** `src/LeanAI.Domain/FoodTracking/Entities/CustomActivityLog.cs`

```csharp
public class CustomActivityLog : BaseEntity
{
    public DateOnly  Date        { get; set; }
    public string    Description { get; set; } = string.Empty;
    public Guid      CaloryLogId { get; set; }
    public CaloryLog CaloryLog   { get; set; } = null!;
}
```

**File:** `src/LeanAI.Domain/FoodTracking/Interfaces/ICustomActivityLogRepository.cs`

```csharp
public interface ICustomActivityLogRepository
{
    Task AddAsync(CustomActivityLog log, CancellationToken ct = default);
    Task DeleteForDateAsync(DateOnly date, CancellationToken ct = default);
}
```

---

### Step 2 — Domain: update `CaloryLog` and `ICaloryLogRepository`

**`CaloryLog.cs`** — add optional description field:
```csharp
public string? Description { get; set; }
```

**`ICaloryLogRepository.cs`** — add methods:
```csharp
Task<CaloryLog> AddActivityAsync(DateOnly date, double calories, string description, CancellationToken ct = default);
Task DeleteActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
Task<IReadOnlyList<ActivityCaloryLogDto>> GetActivityCaloriesForDateAsync(DateOnly date, CancellationToken ct = default);
```
`GetTotalCaloriesAsync` semantics change: returns **net** = Σ food − Σ activity.

---

### Step 3 — Application: new DTOs

**`src/LeanAI.Application/ActivityTracking/DTOs/RunImportResultDto.cs`**
```csharp
public sealed record RunImportResultDto(
    string ActivityText,
    double CaloriesBurned,
    IReadOnlyList<ActivityMetricDto> Metrics);
```

**`src/LeanAI.Application/ActivityTracking/DTOs/RunActivityRowDto.cs`**
```csharp
public sealed record RunActivityRowDto(
    string ActivityText,
    double Calories,
    bool   IsRunRow,
    IReadOnlyList<ActivityMetricDto>? Metrics = null);
```

**`src/LeanAI.Application/FoodTracking/DTOs/ActivityCaloryLogDto.cs`**
```csharp
public record ActivityCaloryLogDto(Guid Id, string? Description, double Calories);
```

---

### Step 4 — Application: new service interface `IActivityCaloriesEstimationService`

**`src/LeanAI.Application/ActivityTracking/Services/IActivityCaloriesEstimationService.cs`**

```csharp
public interface IActivityCaloriesEstimationService
{
    Task<IReadOnlyList<double>> EstimateAsync(
        IReadOnlyList<string> descriptions,
        CancellationToken ct = default);
}
```

---

### Step 5 — Application: `AnalyzeRunImageCommand`

**`src/LeanAI.Application/ActivityTracking/Commands/AnalyzeRunImage/`**

- `AnalyzeRunImageCommand.cs` — `record(byte[] ImageBytes, string MimeType) : IRequest<RunImportResultDto>`
- `AnalyzeRunImageCommandHandler.cs`:
  - Calls `IRunImageAnalysisService.AnalyzeAsync`
  - Builds formatted comment from distance/pace/duration
  - Extracts calories from `calories_burned` metric (defaults to 0 if absent)
  - Returns `RunImportResultDto(activityText, calories, metrics)`

---

### Step 6 — Application: `EstimateActivityCaloriesCommand`

**`src/LeanAI.Application/ActivityTracking/Commands/EstimateActivityCalories/`**

- `EstimateActivityCaloriesCommand.cs` — `record(IReadOnlyList<string> Descriptions) : IRequest<IReadOnlyList<double>>`
- `EstimateActivityCaloriesCommandHandler.cs` — delegates to `IActivityCaloriesEstimationService`

---

### Step 7 — Application: `SaveRunActivitiesCommand`

**`src/LeanAI.Application/ActivityTracking/Commands/SaveRunActivities/`**

- `SaveRunActivitiesCommand.cs` — `record(DateOnly Date, IReadOnlyList<RunActivityRowDto> Rows, bool IsImportMode) : IRequest`
- `SaveRunActivitiesCommandHandler.cs`:
  - Always: `DeleteActivityCaloriesForDateAsync` (full replace, same as food)
  - For each row:
    - `CaloryLog` ← `AddActivityAsync(date, row.Calories, row.ActivityText)`
    - If `row.IsRunRow && IsImportMode`:
      - Save metrics to `ActivityLog` (distance, pace, duration, calories_burned)
      - `AppendActivityCommentCommand(date, row.ActivityText)` via mediator
    - If `!row.IsRunRow`:
      - `CustomActivityLog` ← link to newly created `CaloryLog`

---

### Step 8 — Application: `GetActivityCaloriesForDateQuery`

**`src/LeanAI.Application/FoodTracking/Queries/GetActivityCaloriesForDate/`**

- `GetActivityCaloriesForDateQuery.cs` — `record(DateOnly Date) : IRequest<IReadOnlyList<ActivityCaloryLogDto>>`
- `GetActivityCaloriesForDateQueryHandler.cs` — calls `ICaloryLogRepository.GetActivityCaloriesForDateAsync`

---

### Step 9 — Infrastructure: update `GeminiRunImageAnalysisService`

**Changes:**
- Update `SystemInstruction` to also request `calories_burned`:
  ```
  Each item must have 'parameter_name' (one of: distance, pace, duration, calories_burned), 'value', and 'unit'.
  ```
- `calories_burned` is **optional** — keep `RequiredParameters = ["distance", "pace", "duration"]`
- No changes to `ParseResponse` — extra metrics are returned as-is

---

### Step 10 — Infrastructure: new `GeminiActivityCaloriesEstimationService`

**`src/LeanAI.Infrastructure/ActivityTracking/Services/GeminiActivityCaloriesEstimationService.cs`**

- Implements `IActivityCaloriesEstimationService`
- System prompt: `"Given activity descriptions, estimate calories burned for each. Reply with a JSON array of numbers only. No markdown. Example for ['5km run','30min swim']: [300,250]"`
- Sends descriptions as a JSON array in the user message
- Parses response as `double[]`
- Returns list, defaulting missing/invalid values to 0

---

### Step 11 — Infrastructure: `CustomActivityLogRepository`

**`src/LeanAI.Infrastructure/FoodTracking/Repositories/CustomActivityLogRepository.cs`**

- Implements `ICustomActivityLogRepository`
- `AddAsync` — `DbContext.CustomActivityLogs.Add(log); SaveChanges`
- `DeleteForDateAsync` — delete all `CustomActivityLog` where `Date == date`

---

### Step 12 — Infrastructure: update `CaloryLogRepository`

Add implementations for all new `ICaloryLogRepository` methods:

- `AddActivityAsync` — creates `CaloryLog { SourceType = "activity", Calories, Description, Date }`, saves, returns entity
- `DeleteActivityCaloriesForDateAsync` — deletes all `CaloryLog` where `Date == date && SourceType == "activity"`
- `GetActivityCaloriesForDateAsync` — returns `CaloryLog` rows where `Date == date && SourceType == "activity"` mapped to `ActivityCaloryLogDto`
- Update `GetTotalCaloriesAsync` — return `Σ(food) − Σ(activity)` for the date (floor at 0 or allow negative?)

---

### Step 13 — Infrastructure: `RunImportStateService`

**`src/LeanAI.Infrastructure/ActivityTracking/Services/RunImportStateService.cs`**

Thread-safe singleton, same pattern as `FoodImportStateService`:
```csharp
public sealed class RunImportStateService
{
    private readonly object                       _lock    = new();
    private          IReadOnlyList<RunActivityRowDto>? _pending;

    public bool HasPending { get { lock (_lock) return _pending is not null; } }
    public void Set(IReadOnlyList<RunActivityRowDto> rows) { lock (_lock) _pending = rows; }
    public IReadOnlyList<RunActivityRowDto>? Take() { lock (_lock) { var r = _pending; _pending = null; return r; } }
}
```

---

### Step 14 — Infrastructure: EF Core changes

**`LeanAIDbContext.cs`** — add:
```csharp
public DbSet<CustomActivityLog> CustomActivityLogs => Set<CustomActivityLog>();
```
Configure `CustomActivityLog → CaloryLog` FK with `OnDelete(DeleteBehavior.Cascade)`.
Configure `CaloryLog.Description` as nullable string.

**Migration:** `Add_CustomActivityLogAndCaloryLogDescription`
- Add `Description` (nullable nvarchar) column to `CaloryLogs`
- New `CustomActivityLogs` table: `Id`, `Date`, `Description`, `CaloryLogId` (FK → CaloryLogs, CASCADE)

---

### Step 15 — Infrastructure: DI registration

In `DependencyInjection.cs`:
- Register `RunImportStateService` as singleton
- Register `GeminiActivityCaloriesEstimationService` as `IActivityCaloriesEstimationService`
- Register `CustomActivityLogRepository` as `ICustomActivityLogRepository`

---

### Step 16 — MAUI: update `ImportRunActivity`

**New flow (replaces current toast-based flow):**

```csharp
Task.Run(async () =>
{
    // 1. Provision Gemini key (existing cold-start pattern)
    var apiKey = await SecureStorage.GetAsync(GeminiKeyStorageKey);
    if (!string.IsNullOrEmpty(apiKey))
        services.GetRequiredService<GeminiKeyHolder>().ApiKey = apiKey;

    // 2. Analyze image (no persistence)
    var result = await mediator.Send(new AnalyzeRunImageCommand(imageBytes, mimeType));

    // 3. Store pending import
    var row = new RunActivityRowDto(result.ActivityText, result.CaloriesBurned, IsRunRow: true, result.Metrics);
    services.GetRequiredService<RunImportStateService>().Set([row]);

    // 4. Launch MainActivity
    var intent = new Intent(this, typeof(MainActivity));
    intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
    StartActivity(intent);
    RunOnUiThread(Finish);
});
```

Error handling: show error toast + `Finish()` on exception (same as before).

---

### Step 17 — MAUI: new `RunRowViewModel`

**`src/LeanAI.Maui/ViewModels/RunRowViewModel.cs`**

```csharp
public partial class RunRowViewModel : ObservableObject
{
    [ObservableProperty] private string _activityText    = string.Empty;
    [ObservableProperty] private string _caloriesDisplay = "—";

    public double                             CaloriesValue { get; set; }
    public bool                               IsRunRow      { get; set; }
    public IReadOnlyList<ActivityMetricDto>?  Metrics       { get; set; }

    public void UpdateCalories(double calories)
    {
        CaloriesValue   = calories;
        CaloriesDisplay = calories > 0 ? $"{calories:N0} kcal" : "—";
    }
}
```

---

### Step 18 — MAUI: new `RunReviewViewModel`

**`src/LeanAI.Maui/ViewModels/RunReviewViewModel.cs`**

Mirrors `FoodReviewViewModel`. Key differences:
- `InitialiseAsync(DateOnly date, bool isImportMode)`:
  - Import mode: load from `RunImportStateService.Take()`
  - Edit mode: load from `GetActivityCaloriesForDateQuery`; map each `ActivityCaloryLogDto` to a `RunRowViewModel` with `IsRunRow = false`
- `OnSelectedDateChanged` — edit mode only: reload activity calories for new date
- `ReEvaluateCommand`:
  - Calls `EstimateActivityCaloriesCommand(Items.Select(r => r.ActivityText).ToList())`
  - Updates each row's calories; refreshes total
- `SaveCommand`:
  - Builds `IReadOnlyList<RunActivityRowDto>` from `Items`
  - Calls `SaveRunActivitiesCommand(date, rows, IsImportMode)`
  - `PopModalAsync()`
  - `WeakReferenceMessenger.Default.Send(new RunSavedMessage(targetDate))`
- `AddItemCommand` — adds `new RunRowViewModel { IsRunRow = false }` (custom row)
- `DeleteItemCommand` — removes from `Items`
- `DeleteAllCommand` — confirm dialog → `DeleteActivityCaloriesForDateAsync` equivalent via new command or directly; `PopModalAsync()`
- `CancelCommand` — `PopModalAsync()`
- `TotalCaloriesText` — sum of all row calories

---

### Step 19 — MAUI: new `RunReviewPage`

**`src/LeanAI.Maui/Views/RunReview/RunReviewPage.xaml`**

Structure mirrors `FoodReviewPage.xaml`:
- Grid `RowDefinitions="Auto,*,Auto,Auto"`
- Header: "Run Review" label + `DatePicker`
- CollectionView: each item is a `Border` with a 3-column grid:
  - Col 0 (`*`): `Entry` bound to `ActivityText` (editable)
  - Col 1 (`100`): `Label` bound to `CaloriesDisplay` (read-only, nickel color)
  - Col 2 (`Auto`): delete `Button` — hidden for `IsRunRow = true` rows (`IsVisible="{Binding IsRunRow, Converter={StaticResource InverseBoolConverter}}"`)
- Total row: same `Border` card pattern as food review
- Action buttons (Row 3): Re-evaluate | Save | Delete All | Cancel

**`src/LeanAI.Maui/Views/RunReview/RunReviewPage.xaml.cs`**

```csharp
public partial class RunReviewPage : ContentPage
{
    public RunReviewViewModel ViewModel { get; }
    public RunReviewPage(RunReviewViewModel vm) { InitializeComponent(); BindingContext = ViewModel = vm; }
}
```

---

### Step 20 — MAUI: new `RunSavedMessage`

**`src/LeanAI.Maui/Messages/RunSavedMessage.cs`**
```csharp
public record RunSavedMessage(DateOnly Date);
```

---

### Step 21 — MAUI: new `CaloriesDetailViewModel`

**`src/LeanAI.Maui/ViewModels/CaloriesDetailViewModel.cs`**

```csharp
public partial class CaloriesDetailViewModel(IMediator mediator, IServiceProvider sp)
    : ObservableObject, IRecipient<FoodSavedMessage>, IRecipient<RunSavedMessage>
{
    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private string   _foodTotalText       = "—";
    [ObservableProperty] private string   _activityTotalText   = "—";
    [ObservableProperty] private string   _netCaloriesText     = "—";

    public ObservableCollection<FoodItemDto>        FoodItems     { get; } = new();
    public ObservableCollection<ActivityCaloryLogDto> ActivityItems { get; } = new();

    public async Task InitialiseAsync(DateOnly date)
    {
        Date = date;
        WeakReferenceMessenger.Default.Register<FoodSavedMessage>(this);
        WeakReferenceMessenger.Default.Register<RunSavedMessage>(this);
        await LoadAsync();
    }

    private async Task LoadAsync() { /* load food + activity, compute totals */ }

    [RelayCommand]
    private async Task EditFoodAsync() { /* PushModalAsync(FoodReviewPage, edit mode) */ }

    [RelayCommand]
    private async Task EditActivitiesAsync() { /* PushModalAsync(RunReviewPage, edit mode) */ }

    [RelayCommand]
    private Task CloseAsync() => Shell.Current.Navigation.PopModalAsync();

    void IRecipient<FoodSavedMessage>.Receive(FoodSavedMessage m)
        => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());

    void IRecipient<RunSavedMessage>.Receive(RunSavedMessage m)
        => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
}
```

Net calories displayed as: `(food − activity):N0 kcal`. Can be negative (if activity > food).

---

### Step 22 — MAUI: new `CaloriesDetailPage`

**`src/LeanAI.Maui/Views/CaloriesDetail/CaloriesDetailPage.xaml`**

Three-section layout:
1. **Food** section header ("Food" label + total + "Edit" button) → `CollectionView` of food items (food item name + calories, read-only)
2. **Activities** section header ("Activities" label + total + "Edit" button) → `CollectionView` of activity items (description + calories, read-only)
3. **Net Total** card (copper, bold)
4. Close button at bottom

---

### Step 23 — MAUI: update `LogViewModel`

1. Add `RunImportStateService _runImportState` constructor parameter
2. Implement `IRecipient<RunSavedMessage>` → `Receive` reloads for `message.Date`
3. Register with messenger in constructor: `WeakReferenceMessenger.Default.Register<RunSavedMessage>(this)`
4. In `OnAppearing`, after food-import check, add:
   ```csharp
   if (_runImportState.HasPending)
       await NavigateToRunReviewAsync(isImportMode: true);
   ```
5. Add `NavigateToRunReviewAsync(bool isImportMode)` (same pattern as `NavigateToFoodReviewAsync`)
6. Replace `OpenFoodReviewAsync` (tile command) with `OpenCaloriesDetailAsync`:
   ```csharp
   [RelayCommand]
   private async Task OpenCaloriesDetailAsync()
   {
       var page = _serviceProvider.GetRequiredService<CaloriesDetailPage>();
       await page.ViewModel.InitialiseAsync(EntryDate);
       await Shell.Current.Navigation.PushModalAsync(page);
   }
   ```

---

### Step 24 — MAUI: update `LogPage.xaml`

Change the tile's `TapGestureRecognizer`:
```xml
<TapGestureRecognizer Command="{Binding OpenCaloriesDetailCommand}" />
```

---

### Step 25 — MAUI: DI registration (`MauiProgram.cs`)

Register:
- `RunImportStateService` — singleton
- `RunReviewViewModel`, `RunReviewPage` — transient
- `CaloriesDetailViewModel`, `CaloriesDetailPage` — transient

---

### Step 26 — Tests

New test classes:
- `GeminiRunImageAnalysisServiceTests` — `ParseResponse` with `calories_burned` present/absent
- `AnalyzeRunImageCommandHandlerTests` — calories extracted from metrics
- `EstimateActivityCaloriesCommandHandlerTests`
- `GeminiActivityCaloriesEstimationServiceTests` — `ParseResponse` happy path + invalid JSON
- `SaveRunActivitiesCommandHandlerTests` — import mode (verifies ActivityLog + Notes + CaloryLog), edit mode (verifies delete-then-insert)
- `GetActivityCaloriesForDateQueryHandlerTests`
- `GetTotalCaloriesForDateQueryHandlerTests` — updated: net = food − activity

---

## Definition of Done

- [x] Gemini run analysis returns `calories_burned` (optional 4th metric)
- [x] `ImportRunActivity` stores import state and launches `MainActivity` — no toast, no direct save
- [x] Run Review screen appears after share-sheet import with date picker, activity row, calories column
- [x] Re-evaluate button sends text descriptions to Gemini and populates calories column
- [x] Manual activity rows can be added, edited, deleted
- [x] Save appends commentary to `DailyActualWeight.Notes` (all activity rows, run and custom) and persists calories to `CaloryLog`
- [x] Custom activity rows are persisted in `CustomActivityLog` with FK to `CaloryLog`
- [x] Import mode is additive — food and activity imports accumulate rather than overwriting existing entries
- [x] Daily Log tile shows net calories (food − activity), computed on-the-fly via live DB query; visible even when net is negative
- [x] Calories tile navigates to Calories Detail screen
- [x] Calories Detail shows food section, activity section, and net grand total; totals match tile
- [x] Food Review and Run Review are reachable from Calories Detail in edit mode
- [x] `App.NavigateToLogIfNoEntryTodayAsync` guarded against `ModalStack.Empty` crash on cold-start imports
- [x] `dotnet build` → 0 errors, pre-existing warnings only (SkiaSharp XA0141, MVVMTK0034)
- [x] 178 / 178 tests passing
- [x] `FUNCTIONAL_REQUIREMENTS.md` updated

---

## Files to create (new)

| File | Purpose |
|------|---------|
| `Domain/FoodTracking/Entities/CustomActivityLog.cs` | New entity |
| `Domain/FoodTracking/Interfaces/ICustomActivityLogRepository.cs` | Repository interface |
| `Application/ActivityTracking/DTOs/RunImportResultDto.cs` | Gemini analysis result |
| `Application/ActivityTracking/DTOs/RunActivityRowDto.cs` | Row DTO for state service + command |
| `Application/FoodTracking/DTOs/ActivityCaloryLogDto.cs` | Activity calory query result |
| `Application/ActivityTracking/Services/IActivityCaloriesEstimationService.cs` | Re-evaluate interface |
| `Application/ActivityTracking/Commands/AnalyzeRunImage/AnalyzeRunImageCommand.cs` | Analysis command |
| `Application/ActivityTracking/Commands/AnalyzeRunImage/AnalyzeRunImageCommandHandler.cs` | Analysis handler |
| `Application/ActivityTracking/Commands/EstimateActivityCalories/EstimateActivityCaloriesCommand.cs` | Re-evaluate command |
| `Application/ActivityTracking/Commands/EstimateActivityCalories/EstimateActivityCaloriesCommandHandler.cs` | Re-evaluate handler |
| `Application/ActivityTracking/Commands/SaveRunActivities/SaveRunActivitiesCommand.cs` | Save command |
| `Application/ActivityTracking/Commands/SaveRunActivities/SaveRunActivitiesCommandHandler.cs` | Save handler |
| `Application/FoodTracking/Queries/GetActivityCaloriesForDate/GetActivityCaloriesForDateQuery.cs` | Activity calories query |
| `Application/FoodTracking/Queries/GetActivityCaloriesForDate/GetActivityCaloriesForDateQueryHandler.cs` | Activity calories handler |
| `Infrastructure/ActivityTracking/Services/GeminiActivityCaloriesEstimationService.cs` | Re-evaluate service |
| `Infrastructure/ActivityTracking/Services/RunImportStateService.cs` | Pending import state |
| `Infrastructure/FoodTracking/Repositories/CustomActivityLogRepository.cs` | EF Core repository |
| `Infrastructure/Migrations/..._Add_CustomActivityLogAndCaloryLogDescription.cs` | EF migration |
| `Maui/ViewModels/RunRowViewModel.cs` | Row VM |
| `Maui/ViewModels/RunReviewViewModel.cs` | Review VM |
| `Maui/Views/RunReview/RunReviewPage.xaml` | Review page |
| `Maui/Views/RunReview/RunReviewPage.xaml.cs` | Review page code-behind |
| `Maui/ViewModels/CaloriesDetailViewModel.cs` | Detail VM |
| `Maui/Views/CaloriesDetail/CaloriesDetailPage.xaml` | Detail page |
| `Maui/Views/CaloriesDetail/CaloriesDetailPage.xaml.cs` | Detail page code-behind |
| `Maui/Messages/RunSavedMessage.cs` | Messenger message |

## Files to modify (existing)

| File | Change |
|------|--------|
| `Domain/FoodTracking/Entities/CaloryLog.cs` | Add `Description?` |
| `Domain/FoodTracking/Interfaces/ICaloryLogRepository.cs` | Add 3 methods |
| `Infrastructure/ActivityTracking/Services/GeminiRunImageAnalysisService.cs` | Add `calories_burned` to prompt |
| `Infrastructure/FoodTracking/Repositories/CaloryLogRepository.cs` | Implement new methods; net total |
| `Infrastructure/Persistence/LeanAIDbContext.cs` | Add `CustomActivityLogs` DbSet + config |
| `Infrastructure/DependencyInjection.cs` | Register new services |
| `Maui/Platforms/Android/ImportRunActivity.cs` | New import flow |
| `Maui/ViewModels/LogViewModel.cs` | RunImport + OpenCaloriesDetail |
| `Maui/Views/Log/LogPage.xaml` | Tile tap command |
| `Maui/LeanAI.Maui.csproj` | Register new pages (if needed) |
