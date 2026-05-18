# Phase 9 Implementation Plan: Food Calorie Import — Share Sheet & Daily Tracking ✅ COMPLETE

## Overview

This phase adds a second Android Share Sheet entry point ("Import Food") that accepts a meal photo, sends it to Gemini Vision for food-item extraction, and presents the result in an interactive Food Review screen where the user can edit names/quantities, request a Gemini calorie re-evaluation, and save. Total daily calories appear on the Daily Log screen in a new tappable tile.

---

## Guiding Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| New `FoodTracking` bounded context with `CaloryLog` + `FoodLog` entities | Arch mandates separation from existing contexts. `CaloryLog` is the extensible central ledger — future activity calorie burn adds its own rows without touching `FoodLog`. |
| `FoodLog → CaloryLog` FK (not the reverse) | The user requirement explicitly stated this direction. `FoodLog` is a detail record; `CaloryLog` is the ledger entry it annotates. Cascade delete means removing a `FoodLog` entry (or the full-replace delete) automatically cleans its linked `CaloryLog`. |
| `FoodImportStateService` singleton for cross-Activity handoff | `ImportFoodActivity` cannot push MAUI Shell pages directly (it extends plain `Activity`, not `MauiAppCompatActivity`). The singleton is reachable from both the Activity (via `IPlatformApplication.Current!.Services`) and `LogViewModel` (via DI). Avoids Intent serialisation of complex objects. |
| `ImportFoodActivity` launches `MainActivity`, then `LogViewModel.OnAppearing` navigates | Keeps platform code minimal. `LogViewModel.OnAppearing` already fires on every tab switch, so no extra event bus is needed. |
| `AnalyzeFoodImageCommand` does NOT persist | Keeps the share-sheet path clean. The Activity gets the list back, stores it in `FoodImportStateService`, and the MAUI layer decides what to do with it. |
| `SaveFoodLogCommand` always does a full replace for the date | Simplest correct behaviour for both import-mode save and edit-mode re-save. No partial-update complexity. |
| `RecalculateCaloriesCommand` is text-only (no image) | The original image is not stored and is not needed for recalculation — the food names and quantities carry enough context for Gemini. |
| Modal push for `FoodReviewPage` | Consistent with how `LogPage` is pushed modally from `CalendarViewModel` and how `ImportWizardPage` is pushed from `SettingsViewModel`. |

---

## Step-by-Step Plan

### Step 1 — Domain Layer: `FoodTracking` Bounded Context

**Files to create:**

1. `src/LeanAI.Domain/FoodTracking/Entities/CaloryLog.cs`
   - Extends `BaseEntity`
   - Properties: `DateOnly Date`, `double Calories`, `string SourceType`

2. `src/LeanAI.Domain/FoodTracking/Entities/FoodLog.cs`
   - Extends `BaseEntity`
   - Properties: `DateOnly Date`, `string FoodItem`, `string Quantity`, `Guid CaloryLogId`
   - Navigation property: `CaloryLog CaloryLog` (EF Core loads it; no constructor assignment needed)

3. `src/LeanAI.Domain/FoodTracking/Interfaces/ICaloryLogRepository.cs`
   ```csharp
   Task<double> GetTotalCaloriesAsync(DateOnly date, CancellationToken ct = default);
   ```

4. `src/LeanAI.Domain/FoodTracking/Interfaces/IFoodLogRepository.cs`
   ```csharp
   Task<IReadOnlyList<FoodLog>> GetByDateAsync(DateOnly date, CancellationToken ct = default);
   Task AddRangeAsync(IEnumerable<FoodLog> logs, CancellationToken ct = default);
   Task DeleteByDateAsync(DateOnly date, CancellationToken ct = default);
   ```

---

### Step 2 — Application Layer: DTOs, Service Interface, Commands, Queries

**Files to create:**

5. `src/LeanAI.Application/FoodTracking/DTOs/FoodItemDto.cs`
   ```csharp
   public sealed record FoodItemDto(string FoodItem, string Quantity, double Calories);
   ```

6. `src/LeanAI.Application/FoodTracking/DTOs/FoodItemInputDto.cs`
   ```csharp
   public sealed record FoodItemInputDto(string FoodItem, string Quantity);
   ```

7. `src/LeanAI.Application/FoodTracking/DTOs/FoodLogEntryDto.cs`
   ```csharp
   public sealed record FoodLogEntryDto(Guid FoodLogId, Guid CaloryLogId, string FoodItem, string Quantity, double Calories);
   ```

8. `src/LeanAI.Application/FoodTracking/Services/IFoodImageAnalysisService.cs`
   ```csharp
   Task<IReadOnlyList<FoodItemDto>> AnalyzeImageAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
   Task<IReadOnlyList<FoodItemDto>> RecalculateCaloriesAsync(IReadOnlyList<FoodItemInputDto> items, CancellationToken ct = default);
   ```

9. `src/LeanAI.Application/FoodTracking/Commands/AnalyzeFoodImage/AnalyzeFoodImageCommand.cs`
   ```csharp
   public sealed record AnalyzeFoodImageCommand(byte[] ImageBytes, string MimeType, DateOnly Date)
       : IRequest<IReadOnlyList<FoodItemDto>>;
   ```

10. `src/LeanAI.Application/FoodTracking/Commands/AnalyzeFoodImage/AnalyzeFoodImageCommandHandler.cs`
    - Injects: `IFoodImageAnalysisService`
    - Logic: call `service.AnalyzeImageAsync(ImageBytes, MimeType, ct)` → return result. No DB write.

11. `src/LeanAI.Application/FoodTracking/Commands/SaveFoodLog/SaveFoodLogCommand.cs`
    ```csharp
    public sealed record SaveFoodLogCommand(DateOnly Date, IReadOnlyList<FoodItemDto> Items) : IRequest;
    ```

12. `src/LeanAI.Application/FoodTracking/Commands/SaveFoodLog/SaveFoodLogCommandHandler.cs`
    - Injects: `IFoodLogRepository`
    - Logic:
      1. `await repo.DeleteByDateAsync(request.Date, ct)` — removes existing entries (CaloryLog cascade-deleted)
      2. Build `FoodLog` list: for each `FoodItemDto`, create `new FoodLog { Date = date, FoodItem = item.FoodItem, Quantity = item.Quantity, CaloryLog = new CaloryLog { Date = date, Calories = item.Calories, SourceType = "food" } }`
      3. `await repo.AddRangeAsync(entities, ct)`

13. `src/LeanAI.Application/FoodTracking/Commands/DeleteFoodLog/DeleteFoodLogForDateCommand.cs`
    ```csharp
    public sealed record DeleteFoodLogForDateCommand(DateOnly Date) : IRequest;
    ```

14. `src/LeanAI.Application/FoodTracking/Commands/DeleteFoodLog/DeleteFoodLogForDateCommandHandler.cs`
    - Injects: `IFoodLogRepository`
    - Logic: `await repo.DeleteByDateAsync(request.Date, ct)`

15. `src/LeanAI.Application/FoodTracking/Commands/RecalculateCalories/RecalculateCaloriesCommand.cs`
    ```csharp
    public sealed record RecalculateCaloriesCommand(IReadOnlyList<FoodItemInputDto> Items)
        : IRequest<IReadOnlyList<FoodItemDto>>;
    ```

16. `src/LeanAI.Application/FoodTracking/Commands/RecalculateCalories/RecalculateCaloriesCommandHandler.cs`
    - Injects: `IFoodImageAnalysisService`
    - Logic: `return await service.RecalculateCaloriesAsync(request.Items, ct)`

17. `src/LeanAI.Application/FoodTracking/Queries/GetFoodLogForDate/GetFoodLogForDateQuery.cs`
    ```csharp
    public sealed record GetFoodLogForDateQuery(DateOnly Date) : IRequest<IReadOnlyList<FoodLogEntryDto>>;
    ```

18. `src/LeanAI.Application/FoodTracking/Queries/GetFoodLogForDate/GetFoodLogForDateQueryHandler.cs`
    - Injects: `IFoodLogRepository`
    - Logic: `var logs = await repo.GetByDateAsync(request.Date, ct)` → map each to `FoodLogEntryDto(fl.Id, fl.CaloryLogId, fl.FoodItem, fl.Quantity, fl.CaloryLog.Calories)`

19. `src/LeanAI.Application/FoodTracking/Queries/GetTotalCaloriesForDate/GetTotalCaloriesForDateQuery.cs`
    ```csharp
    public sealed record GetTotalCaloriesForDateQuery(DateOnly Date) : IRequest<double>;
    ```

20. `src/LeanAI.Application/FoodTracking/Queries/GetTotalCaloriesForDate/GetTotalCaloriesForDateQueryHandler.cs`
    - Injects: `ICaloryLogRepository`
    - Logic: `return await repo.GetTotalCaloriesAsync(request.Date, ct)`

---

### Step 3 — Tests (TDD Red — write before infrastructure)

**Files to create in `tests/LeanAI.Tests/`:**

21. `Application/FoodTracking/Commands/AnalyzeFoodImageCommandHandlerTests.cs`
    - **Success:** mock `IFoodImageAnalysisService.AnalyzeImageAsync` returns 3 items → handler returns the same list.
    - **Service throws:** exception propagates from handler.

22. `Application/FoodTracking/Commands/RecalculateCaloriesCommandHandlerTests.cs`
    - **Success:** mock `RecalculateCaloriesAsync` returns updated items → handler returns them.
    - **Service throws:** exception propagates.

23. `Application/FoodTracking/Commands/SaveFoodLogCommandHandlerTests.cs`
    - **Non-empty items:** verifies `DeleteByDateAsync` called first, then `AddRangeAsync` called with correct count and field values (`FoodItem`, `Quantity`, linked `CaloryLog.Calories`, `SourceType = "food"`).
    - **Empty items list:** verifies `DeleteByDateAsync` called, `AddRangeAsync` called with empty list.

24. `Application/FoodTracking/Commands/DeleteFoodLogForDateCommandHandlerTests.cs`
    - **Happy path:** verifies `DeleteByDateAsync` called with correct date.

25. `Application/FoodTracking/Queries/GetFoodLogForDateQueryHandlerTests.cs`
    - **Items found:** mock returns 2 `FoodLog` objects (with populated `CaloryLog`); verifies mapping to `FoodLogEntryDto` is correct.
    - **Empty result:** returns empty list.

26. `Application/FoodTracking/Queries/GetTotalCaloriesForDateQueryHandlerTests.cs`
    - **Has entries:** mock returns `1847.5` → handler returns `1847.5`.
    - **No entries:** mock returns `0.0` → handler returns `0.0`.

27. `Infrastructure/FoodTracking/GeminiFoodImageAnalysisServiceTests.cs`
    - Tests for `GeminiFoodImageAnalysisService.ParseResponse(string)` (internal static, same testability pattern as Phase 8):
    - **Valid JSON array** → returns correct `FoodItemDto` list.
    - **JSON with markdown fences** → fences stripped, parsing succeeds.
    - **Empty array** → throws `InvalidOperationException`.
    - **Item missing `calories` field** → throws `InvalidOperationException`.
    - **Item missing `food_item` field** → throws `InvalidOperationException`.
    - **Malformed JSON** → throws `InvalidOperationException`.

---

### Step 4 — Infrastructure Layer

**Files to create:**

28. `src/LeanAI.Infrastructure/FoodTracking/Services/GeminiFoodImageAnalysisService.cs`
    - `internal sealed class` implementing `IFoodImageAnalysisService`
    - Injects `IChatClient`
    - **`AnalyzeImageAsync`:** multimodal message — `ChatRole.System` instruction asking for JSON array only; `ChatRole.User` as `AIContent[]` containing `DataContent(imageBytes, mimeType)` + `TextContent("List all food items visible in this meal photo.")`
    - **`RecalculateCaloriesAsync`:** text-only message — `ChatRole.System` + `ChatRole.User` with a formatted list of `"- {FoodItem}: {Quantity}"` lines, asking for calorie recalculation in the same JSON format
    - **`internal static IReadOnlyList<FoodItemDto> ParseResponse(string raw)`:**
      1. Strip markdown fences (```json ... ```)
      2. Deserialise as `List<GeminiFoodItemDto>` (internal record with `[JsonPropertyName]` for `food_item`, `quantity`, `calories`)
      3. Validate: non-null, non-empty, all items have non-null/non-empty `food_item` and non-negative `calories`
      4. Throw `InvalidOperationException("Could not extract food items from the provided image.")` on any failure
      5. Return mapped `IReadOnlyList<FoodItemDto>`

29. `src/LeanAI.Infrastructure/FoodTracking/Repositories/CaloryLogRepository.cs`
    - `internal sealed class CaloryLogRepository(LeanAIDbContext context) : ICaloryLogRepository`
    - `GetTotalCaloriesAsync`: `return await context.CaloryLogs.Where(cl => cl.Date == date).SumAsync(cl => cl.Calories, ct)`

30. `src/LeanAI.Infrastructure/FoodTracking/Repositories/FoodLogRepository.cs`
    - `internal sealed class FoodLogRepository(LeanAIDbContext context) : IFoodLogRepository`
    - `GetByDateAsync`: `context.FoodLogs.Include(fl => fl.CaloryLog).Where(fl => fl.Date == date).ToListAsync(ct)`
    - `AddRangeAsync`: `context.FoodLogs.AddRange(logs); await context.SaveChangesAsync(ct)`
    - `DeleteByDateAsync`: load all by date, `context.FoodLogs.RemoveRange(logs); await context.SaveChangesAsync(ct)`

31. `src/LeanAI.Infrastructure/FoodTracking/Services/FoodImportStateService.cs`
    - Singleton. Thread-safe via `lock`.
    ```csharp
    public sealed class FoodImportStateService
    {
        private readonly object _lock = new();
        private IReadOnlyList<FoodItemDto>? _pending;
        public bool HasPending { get { lock (_lock) return _pending is not null; } }
        public void Set(IReadOnlyList<FoodItemDto> items) { lock (_lock) _pending = items; }
        public IReadOnlyList<FoodItemDto>? Take() { lock (_lock) { var r = _pending; _pending = null; return r; } }
    }
    ```

**Files to modify:**

32. `src/LeanAI.Infrastructure/Persistence/LeanAIDbContext.cs`
    - Add usings for `FoodTracking` entities
    - Add `public DbSet<CaloryLog> CaloryLogs => Set<CaloryLog>();`
    - Add `public DbSet<FoodLog> FoodLogs => Set<FoodLog>();`
    - In `OnModelCreating`:
      ```csharp
      modelBuilder.Entity<CaloryLog>(b =>
      {
          b.HasKey(e => e.Id);
          b.Ignore(e => e.DomainEvents);
          b.Property(e => e.Date).HasConversion(...); // same DateOnly pattern
          b.Property(e => e.Calories).IsRequired();
          b.Property(e => e.SourceType).IsRequired();
      });

      modelBuilder.Entity<FoodLog>(b =>
      {
          b.HasKey(e => e.Id);
          b.Ignore(e => e.DomainEvents);
          b.Property(e => e.Date).HasConversion(...);
          b.Property(e => e.FoodItem).IsRequired();
          b.Property(e => e.Quantity).IsRequired();
          b.HasOne(e => e.CaloryLog)
           .WithMany()
           .HasForeignKey(e => e.CaloryLogId)
           .OnDelete(DeleteBehavior.Cascade);
      });
      ```

33. `src/LeanAI.Infrastructure/DependencyInjection.cs`
    - Add `services.AddScoped<ICaloryLogRepository, CaloryLogRepository>();`
    - Add `services.AddScoped<IFoodLogRepository, FoodLogRepository>();`
    - Add `services.AddTransient<IFoodImageAnalysisService, GeminiFoodImageAnalysisService>();`
    - Add `services.AddSingleton<FoodImportStateService>();`

**EF Core Migration:**

34. Run: `dotnet ef migrations add Add_FoodTracking --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Infrastructure`
    - Creates `CaloryLogs` table: `Id` (TEXT PK), `Date` (TEXT NOT NULL), `Calories` (REAL NOT NULL), `SourceType` (TEXT NOT NULL)
    - Creates `FoodLogs` table: `Id` (TEXT PK), `Date` (TEXT NOT NULL), `FoodItem` (TEXT NOT NULL), `Quantity` (TEXT NOT NULL), `CaloryLogId` (TEXT NOT NULL, FK → `CaloryLogs.Id` ON DELETE CASCADE)
    - No existing tables altered

---

### Step 5 — Android Platform: `ImportFoodActivity`

**File to create:**

35. `src/LeanAI.Maui/Platforms/Android/ImportFoodActivity.cs`
    - Class: `public class ImportFoodActivity : Activity`
    - Attributes:
      ```csharp
      [Activity(Label = "Import Food", Exported = true)]
      [IntentFilter(
          new[] { global::Android.Content.Intent.ActionSend },
          Categories = new[] { global::Android.Content.Intent.CategoryDefault },
          DataMimeType = "image/*",
          Label = "Import Food")]
      ```
    - `OnCreate` logic:
      1. `base.OnCreate(savedInstanceState)`
      2. `SetLoadingView()` — same branded programmatic layout as Phase 8 (`#222222` background, copper spinner, nickel label, white app name)
      3. Read `imageUri` from `Intent?.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri` (`#pragma warning disable/restore CA1422`)
      4. If `imageUri` is null → `Finish()` and return
      5. Read MIME type via `ContentResolver!.GetType(imageUri) ?? "image/jpeg"`
      6. Read image bytes into `byte[]` via `ContentResolver.OpenInputStream`
      7. Resolve services: `IMediator`, `FoodImportStateService` from `IPlatformApplication.Current!.Services`
      8. `Task.Run(async () => { ... })`:
         - Provision Gemini key from `SecureStorage` (key `"gemini_key"`) into `GeminiKeyHolder` (same pattern as Phase 8)
         - `var items = await mediator.Send(new AnalyzeFoodImageCommand(imageBytes, mimeType, DateOnly.FromDateTime(DateTime.Today)))`
         - `foodImportStateService.Set(items)`
         - Launch `MainActivity`: `var intent = new Intent(this, typeof(MainActivity)); intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop); StartActivity(intent);`
         - `RunOnUiThread(Finish)`
         - On exception: `RunOnUiThread(() => { Toast.MakeText(this, "Could not analyse the meal — please try again.", ToastLength.Long)!.Show(); Finish(); })`

---

### Step 6 — MAUI: FoodReviewPage + FoodReviewViewModel

**Files to create:**

36. `src/LeanAI.Maui/ViewModels/FoodReviewViewModel.cs`
    - Primary constructor injects `IMediator`, `FoodImportStateService`
    - `[ObservableProperty] ObservableCollection<FoodRowViewModel> Items` — each row holds FoodItem, Quantity, Calories (string for display)
    - `[ObservableProperty] bool IsImportMode` — controls Cancel button visibility
    - `[ObservableProperty] bool IsBusy`
    - `[ObservableProperty] DateOnly EntryDate`
    - `public async Task InitialiseAsync(DateOnly date, bool isImportMode)`
      - Sets `EntryDate` and `IsImportMode`
      - If `isImportMode`: takes items from `FoodImportStateService.Take()`, populates `Items`
      - If edit mode: sends `GetFoodLogForDateQuery(date)` → populates `Items`
    - `[RelayCommand] void AddItem()` — appends a `FoodRowViewModel` with empty name, quantity, calories "—"
    - `[RelayCommand] void DeleteItem(FoodRowViewModel row)` — removes row from `Items`
    - `[RelayCommand] async Task ReEvaluateAsync()` — builds `IReadOnlyList<FoodItemInputDto>` from `Items`, sends `RecalculateCaloriesCommand`, updates `Items` calories in place
    - `[RelayCommand] async Task SaveAsync()` — builds `IReadOnlyList<FoodItemDto>` from `Items`, sends `SaveFoodLogCommand`, navigates back via `Shell.Current.Navigation.PopModalAsync()`
    - `[RelayCommand] async Task DeleteAllAsync()` — confirm dialog → sends `DeleteFoodLogForDateCommand(EntryDate)` → `Items.Clear()` → navigate back
    - `[RelayCommand] Task CancelAsync()` — `Shell.Current.Navigation.PopModalAsync()`

37. `src/LeanAI.Maui/ViewModels/FoodRowViewModel.cs`
    - Lightweight `ObservableObject` for each list row
    - `[ObservableProperty] string foodItem` — editable
    - `[ObservableProperty] string quantity` — editable
    - `[ObservableProperty] string caloriesDisplay` — read-only display text ("—" or "248 kcal")
    - `double CaloriesValue` — backing value for `SaveFoodLogCommand`

38. `src/LeanAI.Maui/Views/FoodReview/FoodReviewPage.xaml`
    - `x:DataType="vm:FoodReviewViewModel"`
    - Header: "Food Review | {EntryDate}" (same style as LogPage header)
    - `CollectionView` bound to `Items`, `SelectionMode="None"`:
      - Each row: `Grid ColumnDefinitions="*,Auto,Auto,Auto"`:
        - Col 0: `Entry` bound to `FoodItem` (food name)
        - Col 1: `Entry` bound to `Quantity` (short, e.g. `WidthRequest="90"`)
        - Col 2: `Label` bound to `CaloriesDisplay`, read-only, Nickel colour
        - Col 3: delete `ImageButton` / `Button` invoking `DeleteItemCommand` with `CommandParameter="{Binding .}"`
    - Buttons row (below list): "Add Item", "Re-evaluate with AI", "Save"
    - "Delete All" button — full width, Nickel background
    - "Cancel" button — visible only when `IsImportMode` is true

39. `src/LeanAI.Maui/Views/FoodReview/FoodReviewPage.xaml.cs`
    - Code-behind: calls `ViewModel.InitialiseAsync(date, isImportMode)` in `OnNavigatedTo` with parameters passed from the caller.

---

### Step 7 — MAUI: LogViewModel Update & Shell Registration

**Files to modify:**

40. `src/LeanAI.Maui/ViewModels/LogViewModel.cs`
    - Add constructor parameter `IServiceProvider serviceProvider` and `FoodImportStateService foodImportState`
    - Add `[ObservableProperty] string _totalCaloriesText = "—";`
    - In `LoadCoreAsync`: after existing query, send `GetTotalCaloriesForDateQuery(date)` → if result > 0, set `TotalCaloriesText = $"{result:N0} kcal"` else `"—"`
    - In `LoadLogAsync` (called via `[RelayCommand]`), after loading, check `foodImportState.HasPending` and if so call `NavigateToFoodReviewAsync(isImportMode: true)`
    - `[RelayCommand] async Task OpenFoodReviewAsync()` — called by tile tap → `NavigateToFoodReviewAsync(isImportMode: false)`
    - `private async Task NavigateToFoodReviewAsync(bool isImportMode)`:
      ```csharp
      var page = _serviceProvider.GetRequiredService<FoodReviewPage>();
      await page.ViewModel.InitialiseAsync(EntryDate, isImportMode);
      await Shell.Current.Navigation.PushModalAsync(page);
      ```

41. `src/LeanAI.Maui/Views/Log/LogPage.xaml`
    - Add a new full-width `Border` card between the two-column analysis grid and "Current Average Weekly Weight":
      ```xml
      <!-- Calories Today -->
      <Border BackgroundColor="#2A2A2A" StrokeThickness="0" Padding="0">
          <Border.StrokeShape><RoundRectangle CornerRadius="10" /></Border.StrokeShape>
          <Border.GestureRecognizers>
              <TapGestureRecognizer Command="{Binding OpenFoodReviewCommand}" />
          </Border.GestureRecognizers>
          <VerticalStackLayout Padding="14,14" Spacing="6">
              <Label Text="Calories Today" TextColor="{StaticResource ColorNickel}" FontSize="13" CharacterSpacing="1" />
              <Label Text="{Binding TotalCaloriesText}"
                     TextColor="{Binding TotalCaloriesText, Converter={StaticResource StringNotEmptyConverter},
                                         ConverterParameter='ColorCopper|ColorNickel'}"
                     FontSize="26" FontAttributes="Bold" />
          </VerticalStackLayout>
      </Border>
      ```
    - **Note:** colour is Copper when a value is shown, Nickel when "—". Use `StringNotEmptyConverter` (already exists from Phase 4.5) or add a converter. If `StringNotEmptyConverter` cannot be repurposed for colour, use a dedicated `[ObservableProperty] bool _hasCalories` on `LogViewModel` with `BoolToColorConverter`.

42. `src/LeanAI.Maui/MauiProgram.cs`
    - Register `builder.Services.AddTransient<FoodReviewPage>();`
    - Register `builder.Services.AddTransient<FoodReviewViewModel>();`
    - Register `builder.Services.AddTransient<FoodRowViewModel>();` (or construct inline in ViewModel)

43. `src/LeanAI.Maui/AppShell.xaml` (optional — modal push does not require Shell route registration)
    - No route needed since `FoodReviewPage` is always pushed modally via `PushModalAsync`, not via `Shell.GoToAsync`.

---

### Step 8 — Run Tests and Verify

44. `dotnet build` → fix any compilation errors
45. `dotnet test` → all tests must pass (existing 140 + new Phase 9 tests)

---

## File Change Summary

| File | Action |
|------|--------|
| `Domain/FoodTracking/Entities/CaloryLog.cs` | CREATE |
| `Domain/FoodTracking/Entities/FoodLog.cs` | CREATE |
| `Domain/FoodTracking/Interfaces/ICaloryLogRepository.cs` | CREATE |
| `Domain/FoodTracking/Interfaces/IFoodLogRepository.cs` | CREATE |
| `Application/FoodTracking/DTOs/FoodItemDto.cs` | CREATE |
| `Application/FoodTracking/DTOs/FoodItemInputDto.cs` | CREATE |
| `Application/FoodTracking/DTOs/FoodLogEntryDto.cs` | CREATE |
| `Application/FoodTracking/Services/IFoodImageAnalysisService.cs` | CREATE |
| `Application/FoodTracking/Commands/AnalyzeFoodImage/AnalyzeFoodImageCommand.cs` | CREATE |
| `Application/FoodTracking/Commands/AnalyzeFoodImage/AnalyzeFoodImageCommandHandler.cs` | CREATE |
| `Application/FoodTracking/Commands/SaveFoodLog/SaveFoodLogCommand.cs` | CREATE |
| `Application/FoodTracking/Commands/SaveFoodLog/SaveFoodLogCommandHandler.cs` | CREATE |
| `Application/FoodTracking/Commands/DeleteFoodLog/DeleteFoodLogForDateCommand.cs` | CREATE |
| `Application/FoodTracking/Commands/DeleteFoodLog/DeleteFoodLogForDateCommandHandler.cs` | CREATE |
| `Application/FoodTracking/Commands/RecalculateCalories/RecalculateCaloriesCommand.cs` | CREATE |
| `Application/FoodTracking/Commands/RecalculateCalories/RecalculateCaloriesCommandHandler.cs` | CREATE |
| `Application/FoodTracking/Queries/GetFoodLogForDate/GetFoodLogForDateQuery.cs` | CREATE |
| `Application/FoodTracking/Queries/GetFoodLogForDate/GetFoodLogForDateQueryHandler.cs` | CREATE |
| `Application/FoodTracking/Queries/GetTotalCaloriesForDate/GetTotalCaloriesForDateQuery.cs` | CREATE |
| `Application/FoodTracking/Queries/GetTotalCaloriesForDate/GetTotalCaloriesForDateQueryHandler.cs` | CREATE |
| `Infrastructure/FoodTracking/Services/GeminiFoodImageAnalysisService.cs` | CREATE |
| `Infrastructure/FoodTracking/Services/FoodImportStateService.cs` | CREATE |
| `Infrastructure/FoodTracking/Repositories/CaloryLogRepository.cs` | CREATE |
| `Infrastructure/FoodTracking/Repositories/FoodLogRepository.cs` | CREATE |
| `Infrastructure/Persistence/LeanAIDbContext.cs` | MODIFY — add `CaloryLogs` + `FoodLogs` DbSets + model config + FK |
| `Infrastructure/DependencyInjection.cs` | MODIFY — register 4 new services |
| `Infrastructure/Migrations/Add_FoodTracking.cs` | GENERATE via `dotnet ef migrations add` |
| `Maui/Platforms/Android/ImportFoodActivity.cs` | CREATE |
| `Maui/ViewModels/FoodReviewViewModel.cs` | CREATE |
| `Maui/ViewModels/FoodRowViewModel.cs` | CREATE |
| `Maui/Views/FoodReview/FoodReviewPage.xaml` | CREATE |
| `Maui/Views/FoodReview/FoodReviewPage.xaml.cs` | CREATE |
| `Maui/ViewModels/LogViewModel.cs` | MODIFY — add `TotalCaloriesText`, `OpenFoodReviewCommand`, import-mode navigation |
| `Maui/Views/Log/LogPage.xaml` | MODIFY — add Calories Today tile |
| `Maui/MauiProgram.cs` | MODIFY — register `FoodReviewPage` + `FoodReviewViewModel` |
| `Tests/Application/FoodTracking/Commands/AnalyzeFoodImageCommandHandlerTests.cs` | CREATE |
| `Tests/Application/FoodTracking/Commands/RecalculateCaloriesCommandHandlerTests.cs` | CREATE |
| `Tests/Application/FoodTracking/Commands/SaveFoodLogCommandHandlerTests.cs` | CREATE |
| `Tests/Application/FoodTracking/Commands/DeleteFoodLogForDateCommandHandlerTests.cs` | CREATE |
| `Tests/Application/FoodTracking/Queries/GetFoodLogForDateQueryHandlerTests.cs` | CREATE |
| `Tests/Application/FoodTracking/Queries/GetTotalCaloriesForDateQueryHandlerTests.cs` | CREATE |
| `Tests/Infrastructure/FoodTracking/GeminiFoodImageAnalysisServiceTests.cs` | CREATE |

**No existing tests are modified. No existing tables are altered.**

---

## Post-Plan Corrections (applied during implementation)

Three additional requirements were clarified after plan approval:

| # | Correction | Implementation |
|---|------------|----------------|
| 1 | Date at top of Food Review must be editable via a `DatePicker` (min: `GoalStartDate`; max: today). In edit mode, changing the date reloads the food log. In import mode, it only shifts the save target. | `UserProfileDto` gained `DateOnly? GoalStartDate`. `FoodReviewViewModel` replaced `_entryDateLabel` with `SelectedDate`/`MinDate`/`MaxDate` (`DateTime`). `FoodReviewPage.xaml` header replaced `Span` with `DatePicker`. |
| 2 | A **total calories** summary row pinned below the food list, updating automatically on add/delete/re-evaluate. | `[ObservableProperty] string TotalCaloriesText`. `RefreshTotal()` sums `Items`. Called after load, re-evaluate, and via `Items.CollectionChanged` subscription. New `Border` card in `FoodReviewPage.xaml` between list and buttons. |
| 3 | After **Save**, the Daily Log must navigate to the date chosen in the picker, not always today. | `FoodSavedMessage(DateOnly)` record. `FoodReviewViewModel.SaveAsync` sends it via `WeakReferenceMessenger` after `PopModalAsync`. `LogViewModel` implements `IRecipient<FoodSavedMessage>` and calls `LoadCoreAsync(message.Date)` on the main thread. |

---

## Definition of Done Checklist

- ✅ Android Share Sheet lists "Import Food" when sharing an image from another app
- ✅ Sharing a meal photo shows the branded loading screen then opens LeanAI's Food Review screen with items from Gemini
- ✅ Each row shows food name (editable), quantity (editable), calories (read-only)
- ✅ Delete row button removes the row from the list
- ✅ "Add item" appends a new empty row
- ✅ "Re-evaluate with AI" sends the full list to Gemini and updates all calorie values in place
- ✅ "Save" stores `FoodLog` + `CaloryLog` rows for the chosen date; total appears on Daily Log tile; Daily Log navigates to that date
- ✅ "Delete All" (with confirmation) deletes all rows; Daily Log tile resets to "—"
- ✅ "Cancel" (import mode only) dismisses without saving
- ✅ Food Review header shows an editable DatePicker (min: GoalStartDate; max: today)
- ✅ Total calories summary row present below food list; updates on every add/delete/re-evaluate
- ✅ Daily Log "Calories Today" tile shows total kcal in Copper (or "—" in Nickel if empty)
- ✅ Tapping "Calories Today" tile opens Food Review in edit mode for today
- ✅ `FoodLog.CaloryLogId` FK with cascade delete verified: deleting a FoodLog row removes its CaloryLog row
- ✅ EF Core migration `Add_FoodTracking` applies cleanly — no existing tables altered
- ✅ `AnalyzeFoodImageCommandHandler`, `RecalculateCaloriesCommandHandler`, `SaveFoodLogCommandHandler`, `DeleteFoodLogForDateCommandHandler` at 100% branch coverage
- ✅ `GetFoodLogForDateQueryHandler`, `GetTotalCaloriesForDateQueryHandler` at 100% branch coverage
- ✅ `GeminiFoodImageAnalysisService.ParseResponse` tested: valid, fenced, empty, missing fields, malformed
- ✅ `dotnet build` → 0 errors, 0 warnings
- ✅ `dotnet test` → 157 / 157 tests green
