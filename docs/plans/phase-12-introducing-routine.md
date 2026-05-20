# Phase 12: Introducing Routine — Implementation Plan

## Status: ✅ COMPLETE (2026-05-21)

## Overview

Phase 12 adds a **Routine** feature that lets users designate recurring food and activity items as shortcuts for daily data entry. The routine can be applied to any day with a single switch on the Calories Detail screen, and can be fully managed via a new screen in Settings.

**Key design decisions (from clarification):**
- "Modified" = any field changed from the original routine value (name, qty, or calories).
- Routine food items → Food section; routine activity items → Activity section (no separate routine section).
- New dedicated `RoutineManagementPage` (not a re-use of `CaloriesDetailPage`).
- Calories stored as a fixed snapshot at the time of routine creation.

---

## Step 1 — Domain Layer: New Entities and Repository Interface

### 1.1 New Entity: `RoutineItem`

**File:** `src/LeanAI.Domain/FoodTracking/Entities/RoutineItem.cs`

```
public class RoutineItem : BaseEntity
{
    public string  SourceType  { get; set; } = string.Empty;  // "food" | "activity"
    public string  Description { get; set; } = string.Empty;  // food item name OR activity description
    public string? Quantity    { get; set; }                  // food only
    public double  Calories    { get; set; }
}
```

### 1.2 New Entity: `DailyRoutineStatus`

**File:** `src/LeanAI.Domain/FoodTracking/Entities/DailyRoutineStatus.cs`

```
public class DailyRoutineStatus : BaseEntity
{
    public DateOnly Date     { get; set; }
    public bool     IsActive { get; set; }
}
```

### 1.3 CaloryLog Schema Addition

**File:** `src/LeanAI.Domain/FoodTracking/Entities/CaloryLog.cs`

Add a new nullable property:
```
public Guid? RoutineItemId { get; set; }
```
This is a bare Guid (no FK constraint) that records which `RoutineItem` produced this entry when the routine was applied.

### 1.4 New Repository Interface: `IRoutineRepository`

**File:** `src/LeanAI.Domain/FoodTracking/Interfaces/IRoutineRepository.cs`

```
Task<IReadOnlyList<RoutineItem>> GetAllAsync(CancellationToken ct = default);
Task ReplaceAllAsync(IReadOnlyList<RoutineItem> items, CancellationToken ct = default);
Task<bool> GetIsActiveForDateAsync(DateOnly date, CancellationToken ct = default);
Task SetIsActiveForDateAsync(DateOnly date, bool isActive, CancellationToken ct = default);
```

---

## Step 2 — Infrastructure Layer: Repository, DB Context & Migration

### 2.1 Update `LeanAIDbContext`

**File:** `src/LeanAI.Infrastructure/Persistence/LeanAIDbContext.cs`

Add DbSets:
```csharp
public DbSet<RoutineItem>        RoutineItems        => Set<RoutineItem>();
public DbSet<DailyRoutineStatus> DailyRoutineStatuses => Set<DailyRoutineStatus>();
```

Add `OnModelCreating` configurations:

```csharp
modelBuilder.Entity<RoutineItem>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Ignore(e => e.DomainEvents);
    entity.Property(e => e.SourceType).IsRequired();
    entity.Property(e => e.Description).IsRequired();
    entity.Property(e => e.Quantity);
    entity.Property(e => e.Calories).IsRequired();
});

modelBuilder.Entity<DailyRoutineStatus>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Ignore(e => e.DomainEvents);
    entity.Property(e => e.Date).IsRequired()
          .HasConversion(d => d.ToString("yyyy-MM-dd"), s => DateOnly.Parse(s));
    entity.HasIndex(e => e.Date).IsUnique();
    entity.Property(e => e.IsActive).IsRequired();
});
```

For `CaloryLog` entity configuration, add:
```csharp
entity.Property(e => e.RoutineItemId);  // nullable Guid, no FK
```

### 2.2 New Repository: `RoutineRepository`

**File:** `src/LeanAI.Infrastructure/FoodTracking/Repositories/RoutineRepository.cs`

```csharp
internal sealed class RoutineRepository(LeanAIDbContext context) : IRoutineRepository
```

Implement all four interface methods:
- `GetAllAsync` → `context.RoutineItems.OrderBy(r => r.SourceType).ThenBy(r => r.Id).ToListAsync(ct)`
- `ReplaceAllAsync` → delete all existing, add provided items, `SaveChangesAsync`
- `GetIsActiveForDateAsync` → find row by date, return `IsActive` (false if not found)
- `SetIsActiveForDateAsync` → upsert: find existing row or create new, set `IsActive`, `SaveChangesAsync`

### 2.3 EF Core Migration

Run: `dotnet ef migrations add Add_Routine_Tables --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Maui`

Expected changes:
- New table `RoutineItems` (`Id`, `SourceType`, `Description`, `Quantity` nullable, `Calories`)
- New table `DailyRoutineStatuses` (`Id`, `Date`, `IsActive`) with unique index on `Date`
- New nullable column `RoutineItemId` on `CaloryLogs`

Verify the migration does **not** drop or alter any existing tables.

### 2.4 DI Registration

**File:** `src/LeanAI.Infrastructure/DependencyInjection.cs`

Add:
```csharp
services.AddScoped<IRoutineRepository, RoutineRepository>();
```

---

## Step 3 — Application Layer: DTOs, Commands, Queries (TDD — tests first)

### 3.1 DTO: `RoutineItemDto`

**File:** `src/LeanAI.Application/FoodTracking/DTOs/RoutineItemDto.cs`

```csharp
public record RoutineItemDto(
    Guid    Id,
    string  SourceType,
    string  Description,
    string? Quantity,
    double  Calories
);
```

### 3.2 `GetRoutineItemsQuery` + Handler

**Query file:** `src/LeanAI.Application/FoodTracking/Queries/GetRoutineItems/GetRoutineItemsQuery.cs`
```csharp
public record GetRoutineItemsQuery : IRequest<IReadOnlyList<RoutineItemDto>>;
```

**Handler:** `GetRoutineItemsQueryHandler`
- Injects `IRoutineRepository`
- Returns mapped `IReadOnlyList<RoutineItemDto>`

### 3.3 `GetRoutineStatusForDateQuery` + Handler

**Query file:** `src/LeanAI.Application/FoodTracking/Queries/GetRoutineStatusForDate/GetRoutineStatusForDateQuery.cs`
```csharp
public record GetRoutineStatusForDateQuery(DateOnly Date) : IRequest<bool>;
```

**Handler:** returns `await _routineRepository.GetIsActiveForDateAsync(request.Date, ct)`

### 3.4 `SaveRoutineFromDayCommand` + Handler

**Purpose:** Called from the CaloriesDetail "Save Routine" button. Replaces the routine store with the provided checked items.

```csharp
public record SaveRoutineFromDayCommand(
    IReadOnlyList<RoutineItemDto> Items
) : IRequest;
```

**Handler:** `SaveRoutineFromDayCommandHandler`
- Injects `IRoutineRepository`
- Converts DTOs to `RoutineItem` entities
- Calls `ReplaceAllAsync`

### 3.5 `ApplyRoutineForDateCommand` + Handler

**Purpose:** Called when the Current Routine switch is toggled ON. Copies routine items to the date.

```csharp
public record ApplyRoutineForDateCommand(DateOnly Date) : IRequest;
```

**Handler:** `ApplyRoutineForDateCommandHandler`
- Injects `IRoutineRepository`, `IFoodLogRepository`, `ICaloryLogRepository`, `ICustomActivityLogRepository`  
  *(or directly via `LeanAIDbContext` if simpler — use existing repository pattern)*
- Load all `RoutineItem`s via `IRoutineRepository.GetAllAsync`
- If empty: call `SetIsActiveForDateAsync(date, true)` and return
- For each food item (`SourceType == "food"`):
  - Create `CaloryLog { Date, Calories = item.Calories, SourceType = "food", RoutineItemId = item.Id }`
  - Create `FoodLog { Date, FoodItem = item.Description, Quantity = item.Quantity ?? "", CaloryLogId = caloryLog.Id }`
- For each activity item (`SourceType == "activity"`):
  - Create `CaloryLog { Date, Calories = item.Calories, SourceType = "activity", Description = item.Description, RoutineItemId = item.Id }`
  - Create `CustomActivityLog { Date, Description = item.Description, CaloryLogId = caloryLog.Id }`
- Save all via respective repositories
- Call `SetIsActiveForDateAsync(date, true)`

**Note:** The handler must work through existing repository interfaces to maintain layer separation. Alternatively, given the cross-entity nature, it may inject `LeanAIDbContext` directly as done in other infrastructure-touching handlers. Follow the pattern established by `SaveFoodLogCommandHandler`.

### 3.6 `RemoveUnmodifiedRoutineItemsForDateCommand` + Handler

**Purpose:** Called when the Current Routine switch is toggled OFF. Compares applied items against originals and deletes unmodified ones.

```csharp
public record RemoveUnmodifiedRoutineItemsForDateCommand(DateOnly Date) : IRequest;
```

**Handler:** `RemoveUnmodifiedRoutineItemsForDateCommandHandler`
- Injects `IRoutineRepository`, `ICaloryLogRepository` (and access to FoodLogs/CustomActivityLogs)
- Load all `CaloryLog` entries for the date where `RoutineItemId IS NOT NULL`
- Load all `RoutineItem`s by their IDs (one batch query)
- For each CaloryLog entry:
  - Look up the `RoutineItem` by `CaloryLog.RoutineItemId`
  - If RoutineItem not found (routine was modified since): skip (preserve the CaloryLog)
  - If `SourceType == "food"`:
    - Load associated `FoodLog` by `CaloryLogId`
    - If `CaloryLog.Calories == routine.Calories AND FoodLog.FoodItem == routine.Description AND FoodLog.Quantity == (routine.Quantity ?? "")`: **delete** CaloryLog (cascade deletes FoodLog)
  - If `SourceType == "activity"`:
    - If `CaloryLog.Calories == routine.Calories AND CaloryLog.Description == routine.Description`: **delete** CaloryLog (cascade deletes CustomActivityLog)
- Call `SetIsActiveForDateAsync(date, false)`

### 3.7 `SaveRoutineItemsCommand` + Handler

**Purpose:** Called from `RoutineManagementPage` Save after Gemini evaluation. Equivalent to `SaveRoutineFromDayCommand`.

```csharp
public record SaveRoutineItemsCommand(
    IReadOnlyList<RoutineItemDto> Items
) : IRequest;
```

**Handler:** Identical behavior to `SaveRoutineFromDayCommandHandler` — reuse or share. Can be the same handler type or a thin wrapper that delegates.

*Design note: Both commands do the same thing. Consider making `SaveRoutineFromDayCommand` and `SaveRoutineItemsCommand` use the same handler class, or simply make `SaveRoutineItemsCommand` an alias. The distinction is conceptual (day-level save vs. management screen save) but the persistence logic is identical.*

---

## Step 4 — Tests for All New Handlers

**File:** `tests/LeanAI.Tests/Application/FoodTracking/`

All handlers require 100% branch coverage with Moq + FluentAssertions.

### `GetRoutineItemsQueryHandlerTests`
- Empty routine → returns empty list
- Non-empty routine → returns mapped DTOs

### `GetRoutineStatusForDateQueryHandlerTests`
- No status row for date → returns `false`
- Status row exists with `IsActive = true` → returns `true`
- Status row exists with `IsActive = false` → returns `false`

### `SaveRoutineFromDayCommandHandlerTests`
- Empty list → `ReplaceAllAsync` called with empty list
- Non-empty list → `ReplaceAllAsync` called with correctly mapped `RoutineItem` entities

### `ApplyRoutineForDateCommandHandlerTests`
- Empty routine → no food/activity log writes; `SetIsActiveForDateAsync(date, true)` called
- Food-only routine → correct `CaloryLog` + `FoodLog` entries created with `RoutineItemId` set; status set to active
- Activity-only routine → correct `CaloryLog` + `CustomActivityLog` entries created; status set to active
- Mixed routine → both types created correctly

### `RemoveUnmodifiedRoutineItemsForDateCommandHandlerTests`
- No CaloryLogs with RoutineItemId for date → no deletes; status set to inactive
- Food item unmodified (all fields match) → CaloryLog deleted; status set to inactive
- Food item modified (calories changed) → CaloryLog preserved; status set to inactive
- Food item modified (food name changed) → CaloryLog preserved
- Food item modified (quantity changed) → CaloryLog preserved
- Activity item unmodified → CaloryLog deleted
- Activity item modified (description changed) → CaloryLog preserved
- Activity item modified (calories changed) → CaloryLog preserved
- Routine item no longer exists (deleted since application) → CaloryLog preserved (treated as modified)

### `SaveRoutineItemsCommandHandlerTests`
- Same branches as `SaveRoutineFromDayCommandHandlerTests`

---

## Step 5 — Presentation: CaloriesDetailPage & ViewModel Changes

### 5.1 New Wrapper ViewModels

**File:** `src/LeanAI.Maui/ViewModels/CaloriesDetailFoodItemViewModel.cs`

```csharp
public partial class CaloriesDetailFoodItemViewModel(FoodLogEntryDto dto) : ObservableObject
{
    public FoodLogEntryDto Dto { get; } = dto;
    [ObservableProperty] private bool _isRoutineChecked;
    public bool IsRoutineEnabled { get; set; } = true;
}
```

**File:** `src/LeanAI.Maui/ViewModels/CaloriesDetailActivityItemViewModel.cs`

```csharp
public partial class CaloriesDetailActivityItemViewModel(ActivityCaloryLogDto dto) : ObservableObject
{
    public ActivityCaloryLogDto Dto { get; } = dto;
    [ObservableProperty] private bool _isRoutineChecked;
    public bool IsRoutineEnabled { get; set; } = true;
}
```

### 5.2 CaloriesDetailViewModel Changes

**File:** `src/LeanAI.Maui/ViewModels/CaloriesDetailViewModel.cs`

New observable properties:
```csharp
[ObservableProperty] private bool _isRoutineActive;
[ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SaveRoutineCommand))]
                     private bool _hasAnyChecked;
```

Computed:
```csharp
public bool AreCheckboxesEnabled => !IsRoutineActive;
```

Change collections:
```csharp
public ObservableCollection<CaloriesDetailFoodItemViewModel>     FoodItemVMs     { get; } = new();
public ObservableCollection<CaloriesDetailActivityItemViewModel> ActivityItemVMs { get; } = new();
```

**`InitialiseAsync` update:**
- After loading food/activity data, load routine status via `GetRoutineStatusForDateQuery`
- Set `IsRoutineActive` accordingly
- When populating collections, set `IsRoutineEnabled = !IsRoutineActive` on each wrapper VM

**`partial void OnIsRoutineActiveChanged(bool value)`:**
- If `_isLoading` (initial load), return
- If `value == true`: send `ApplyRoutineForDateCommand(_date)` → reload data
- If `value == false`: send `RemoveUnmodifiedRoutineItemsForDateCommand(_date)` → reload data
- Update `IsRoutineEnabled` on all item VMs after reload
- Notify `AreCheckboxesEnabled` changed

**`SaveRoutineCommand` (with CanExecute = `HasAnyChecked && !IsRoutineActive`):**
- Collect checked food items → `RoutineItemDto` list (SourceType = "food")
- Collect checked activity items → `RoutineItemDto` list (SourceType = "activity")
- Send `SaveRoutineFromDayCommand(combined list)`
- Reset all `IsRoutineChecked` to false, update `HasAnyChecked`

**Checkbox change subscription:**
- When a wrapper VM's `IsRoutineChecked` changes, recompute `HasAnyChecked`
- Subscribe via `ObservableCollection.CollectionChanged` and per-item `PropertyChanged`

**`LoadAsync` update:**
- After loading, set `IsRoutineEnabled` on each wrapper VM based on `IsRoutineActive`

### 5.3 CaloriesDetailPage.xaml Changes

**File:** `src/LeanAI.Maui/Views/CaloriesDetail/CaloriesDetailPage.xaml`

**Header row:** Add a "Save Routine" button next to the ✕ close button, visible only when `!IsRoutineActive`:
```xml
<Button
    Grid.Column="1"
    Text="Save Routine"
    IsVisible="{Binding AreCheckboxesEnabled}"
    IsEnabled="{Binding HasAnyChecked}"
    Command="{Binding SaveRoutineCommand}" ... />
```
*(Adjust column definitions to accommodate both buttons)*

**After the header Grid, before the ScrollView content:** Add the switch row:
```xml
<Grid ColumnDefinitions="*,Auto" Padding="24,8,24,0">
    <Label Text="Current Routine" TextColor="{StaticResource ColorText}" FontSize="15" VerticalOptions="Center" />
    <Switch IsToggled="{Binding IsRoutineActive}"
            OnColor="{StaticResource ColorCopper}"
            Grid.Column="1" VerticalOptions="Center" />
</Grid>
```

**Food items section:** 
- Add a "Routine" column header row above the CollectionView (aligned right)
- Change `ItemsSource` to `FoodItemVMs`
- Change DataTemplate type to `vm:CaloriesDetailFoodItemViewModel`
- Add `CheckBox` bound to `IsRoutineChecked`, `IsEnabled` bound to `IsRoutineEnabled`

**Activity items section:**
- Same pattern as food section
- Change `ItemsSource` to `ActivityItemVMs`

**BMR section:**
- Add a disabled CheckBox (always unchecked, `IsEnabled="False"`) to match the visual pattern:
```xml
<CheckBox IsChecked="False" IsEnabled="False" ... />
```

---

## Step 6 — Presentation: New Routine Management Screen

### 6.1 `RoutineFoodItemViewModel`

**File:** `src/LeanAI.Maui/ViewModels/RoutineFoodItemViewModel.cs`

```csharp
public partial class RoutineFoodItemViewModel : ObservableObject
{
    public Guid? OriginalId { get; init; }
    [ObservableProperty] private string _foodItem   = string.Empty;
    [ObservableProperty] private string _quantity   = string.Empty;
    [ObservableProperty] private double _calories;
    [ObservableProperty] private bool   _isChecked  = true;

    public string CaloriesText => Calories > 0 ? $"{Calories:N0} kcal" : "—";
}
```

### 6.2 `RoutineActivityItemViewModel`

**File:** `src/LeanAI.Maui/ViewModels/RoutineActivityItemViewModel.cs`

```csharp
public partial class RoutineActivityItemViewModel : ObservableObject
{
    public Guid? OriginalId { get; init; }
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private double _calories;
    [ObservableProperty] private bool   _isChecked  = true;

    public string CaloriesText => Calories > 0 ? $"{Calories:N0} kcal" : "—";
}
```

### 6.3 `RoutineManagementViewModel`

**File:** `src/LeanAI.Maui/ViewModels/RoutineManagementViewModel.cs`

```csharp
public partial class RoutineManagementViewModel(IMediator mediator) : ObservableObject
```

Collections:
```csharp
public ObservableCollection<RoutineFoodItemViewModel>     FoodItems     { get; } = new();
public ObservableCollection<RoutineActivityItemViewModel> ActivityItems { get; } = new();
```

`InitialiseAsync`:
- Send `GetRoutineItemsQuery`
- Populate `FoodItems` (SourceType = "food") and `ActivityItems` (SourceType = "activity")
- All items start with `IsChecked = true`

`AddFoodItemCommand`:
- Append new `RoutineFoodItemViewModel` with `IsChecked = true`, empty fields

`AddActivityItemCommand`:
- Append new `RoutineActivityItemViewModel` with `IsChecked = true`, empty fields

`SaveCommand`:
1. Collect checked food items and checked activity items.
2. For checked food items where `Calories == 0`:
   - Send `RecalculateCaloriesCommand(foodInputs)` → update `Calories` on each VM.
3. For checked activity items where `Calories == 0`:
   - Send `EstimateActivityCaloriesCommand(descriptions)` → update `Calories` on each VM.
4. Build `IReadOnlyList<RoutineItemDto>` from all checked items (food + activity).
5. Send `SaveRoutineItemsCommand(items)`.
6. `await Shell.Current.Navigation.PopModalAsync()`

`CancelCommand`:
- `await Shell.Current.Navigation.PopModalAsync()`

### 6.4 `RoutineManagementPage.xaml`

**File:** `src/LeanAI.Maui/Views/Routine/RoutineManagementPage.xaml`

Layout:
```
Header: "Manage Routine" title + Cancel button (top right)
ScrollView:
  VerticalStackLayout:
    "FOOD" section label
    CollectionView (FoodItems) — each row: [CheckBox | FoodItem Entry | Quantity Entry | CaloriesText]
    "Add Food Item" button
    "ACTIVITIES" section label
    CollectionView (ActivityItems) — each row: [CheckBox | Description Entry | CaloriesText]
    "Add Activity" button
Fixed bottom bar:
  "Save" button (full width, Copper background)
```

Style consistent with existing screens (dark `#2A2A2A` cards, Copper accent `#D28B5C`, Nickel labels `#9A9EAB`).

**Code-behind:** `RoutineManagementPage.xaml.cs`

```csharp
public RoutineManagementPage(RoutineManagementViewModel viewModel)
{
    InitializeComponent();
    BindingContext = ViewModel = viewModel;
}
public RoutineManagementViewModel ViewModel { get; }
public async Task InitialiseAsync() => await ViewModel.InitialiseAsync();
```

### 6.5 DI Registration

**File:** `src/LeanAI.Maui/MauiProgram.cs` (or DependencyInjection.cs)

```csharp
services.AddTransient<RoutineManagementPage>();
services.AddTransient<RoutineManagementViewModel>();
```

---

## Step 7 — Settings: Routines Action

### 7.1 SettingsPage.xaml

**File:** `src/LeanAI.Maui/Views/Settings/SettingsPage.xaml`

Add a new action row to the **GENERIC SETTINGS** section, directly after the "Use BMR" card:

```xml
<Border BackgroundColor="#2A2A2A" StrokeThickness="0" Padding="0" Margin="0,8,0,0">
    <Border.StrokeShape><RoundRectangle CornerRadius="10" /></Border.StrokeShape>
    <Grid Padding="16,14" ColumnDefinitions="*,Auto">
        <VerticalStackLayout Grid.Column="0" Spacing="3">
            <Label Text="Routines" TextColor="{StaticResource ColorText}" FontSize="16" FontAttributes="Bold" />
            <Label Text="Manage your recurring food and activity items" TextColor="{StaticResource ColorNickel}" FontSize="13" />
        </VerticalStackLayout>
        <Label Grid.Column="1" Text="›" TextColor="{StaticResource ColorNickel}" FontSize="22" VerticalOptions="Center" />
        <Grid.GestureRecognizers>
            <TapGestureRecognizer Command="{Binding OpenRoutinesCommand}" />
        </Grid.GestureRecognizers>
    </Grid>
</Border>
```

### 7.2 SettingsViewModel

**File:** `src/LeanAI.Maui/ViewModels/SettingsViewModel.cs`

Add command:

```csharp
[RelayCommand]
private async Task OpenRoutinesAsync()
{
    var page = _services.GetRequiredService<RoutineManagementPage>();
    await page.InitialiseAsync();
    await Shell.Current.Navigation.PushModalAsync(page);
}
```

---

## Step 8 — EF Core Migration Execution & Snapshot Update

1. Run: `dotnet ef migrations add Add_Routine_Tables --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Maui`
2. Inspect the generated migration: verify `RoutineItems`, `DailyRoutineStatuses` tables are created, `RoutineItemId` column added to `CaloryLogs`.
3. Verify the model snapshot is updated.
4. Run: `dotnet build` → 0 errors.
5. Run: `dotnet test` → all existing tests green.

---

## Step 9 — Integration Verification

Before marking the phase done:

1. **Happy path — setting a routine:**
   - Open CaloriesDetail for today (switch OFF)
   - Check a food item and an activity item
   - Tap "Save Routine" → checkboxes reset
   - Open Settings → Routines → both items appear checked

2. **Happy path — applying the routine:**
   - Open CaloriesDetail for a different day (empty)
   - Toggle Current Routine ON → routine items appear in food and activity sections
   - Items have correct calories from snapshot

3. **Happy path — turning off routine:**
   - Toggle Current Routine OFF on the above day
   - Items that were not modified are deleted
   - Net Calories tile updates

4. **Modified item preserved:**
   - Apply routine, then Edit Food (FoodReview), change a food name and save
   - Toggle routine OFF → the modified item stays; only unmodified routine items are deleted

5. **Routine management:**
   - Settings → Routines → uncheck an item, add a new food item (no calories), tap Save
   - New item's calories are evaluated by Gemini before save
   - Verify routine store updated correctly

---

## Implementation Notes

- **`CaloriesTotalChangedMessage`** added to `src/LeanAI.Maui/Messages/` so that toggling the routine switch on `CaloriesDetailViewModel` refreshes the Calories tile on `LogViewModel` without a full log reload.
- **`SaveRoutineItemsCommand`** was not implemented as a separate command — `RoutineManagementPage` reuses `SaveRoutineFromDayCommand` (identical behavior).
- **EF Core migration** was run without `--startup-project` flag due to MAUI multi-framework build constraints: `dotnet ef migrations add Add_Routine_Tables --project src/LeanAI.Infrastructure`.

## Definition of Done

- ✅ `RoutineItem`, `DailyRoutineStatus` entities in Domain layer.
- ✅ `CaloryLog.RoutineItemId` nullable Guid added.
- ✅ `IRoutineRepository` defined and implemented by `RoutineRepository`.
- ✅ EF Core migration `Add_Routine_Tables` applies cleanly — no existing tables altered.
- ✅ All new Application handlers implemented and tested at 100% branch coverage.
- ✅ `CaloriesDetailPage`: "Current Routine" switch present; Routine checkboxes on food/activity rows; BMR checkbox disabled; "Save Routine" button visible when switch is OFF.
- ✅ Toggling switch ON adds routine copies (with RoutineItemId set); toggling OFF removes unmodified copies.
- ✅ Routine switch toggle refreshes Calories tile on Daily Log via `CaloriesTotalChangedMessage`.
- ✅ Routine checkboxes are disabled when switch is ON.
- ✅ "Save Routine" button enabled only when ≥1 checkbox checked; saves and resets checkboxes.
- ✅ Settings → Routines action row visible and navigates to `RoutineManagementPage`.
- ✅ `RoutineManagementPage`: shows current routine items pre-checked; allows add/uncheck; Gemini evaluates items with no calories; Save replaces routine store.
- ✅ `dotnet build` → 0 errors. `dotnet test` → all tests green.
