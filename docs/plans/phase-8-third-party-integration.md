# Phase 8 Implementation Plan: Third-Party Integration — Import Run via Share Sheet

## Overview

This phase exposes an Android Share Sheet entry point that accepts a run-tracking screenshot from any third-party app, sends it to Gemini Vision for metric extraction, stores the results in a new `ActivityLogs` table, and appends a human-readable summary to today's weight log comment.

---

## Guiding Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| New `ActivityTracking` bounded context | Arch mandates separation from `WeightManagement`. Cross-context communication goes through MediatR only. |
| `ImportRunActivity` extends plain `Activity` (not `MauiAppCompatActivity`) | MAUI shell/navigation must not spin up for a share-sheet intent. The DI container is still reachable via `IPlatformApplication.Current!.Services` because `MainApplication` initialises the host before any Activity. |
| `ImportRunCommandHandler` dispatches `AppendActivityCommentCommand` via `IMediator` | Keeps the `ActivityTracking` handler free of direct dependencies on `WeightManagement` repositories. |
| `WeightKg = 0` for auto-created weight entries | `DailyActualWeight.WeightKg` is non-nullable `double`; `0` is the chosen sentinel for "weight not logged yet". The comment still carries full run data. |
| Gemini Vision via `DataContent` in `Microsoft.Extensions.AI` | The existing `IChatClient` abstraction already supports multimodal messages; no new packages required. |

---

## Step-by-Step Plan

### Step 1 — Domain Layer: `ActivityTracking` Bounded Context

**Files to create:**

1. `src/LeanAI.Domain/ActivityTracking/Entities/ActivityLog.cs`
   - Extends `BaseEntity`
   - Properties: `DateOnly Date`, `string Activity`, `string ParameterName`, `string Value`, `string Unit`

2. `src/LeanAI.Domain/ActivityTracking/Interfaces/IActivityLogRepository.cs`
   - Single method: `Task AddRangeAsync(IEnumerable<ActivityLog> logs, CancellationToken ct)`

---

### Step 2 — Application Layer: DTOs, Service Interface, Commands

**Files to create:**

3. `src/LeanAI.Application/ActivityTracking/DTOs/ActivityMetricDto.cs`
   - `sealed record ActivityMetricDto(string ParameterName, string Value, string Unit)`

4. `src/LeanAI.Application/ActivityTracking/Services/IRunImageAnalysisService.cs`
   - `Task<IReadOnlyList<ActivityMetricDto>> AnalyzeAsync(byte[] imageBytes, string mimeType, CancellationToken ct)`

5. `src/LeanAI.Application/ActivityTracking/Commands/ImportRun/ImportRunCommand.cs`
   - `sealed record ImportRunCommand(byte[] ImageBytes, string MimeType, DateOnly Date) : IRequest<IReadOnlyList<ActivityMetricDto>>`

6. `src/LeanAI.Application/ActivityTracking/Commands/ImportRun/ImportRunCommandHandler.cs`
   - Injects: `IRunImageAnalysisService`, `IActivityLogRepository`, `IMediator`
   - Logic:
     1. Call `IRunImageAnalysisService.AnalyzeAsync(ImageBytes, MimeType, ct)` → `metrics`
     2. Map `metrics` to `ActivityLog` rows (`Activity = "run"`, `Date = request.Date`)
     3. Call `IActivityLogRepository.AddRangeAsync(logs, ct)`
     4. Build the comment string (see format below)
     5. Dispatch `AppendActivityCommentCommand(request.Date, comment)` via `IMediator`
     6. Return `metrics`

   **Comment format:**
   ```
   I run for {distance_value}{distance_unit} at a pace of {pace_value}{pace_unit}. Total time: {duration_value}{duration_unit}.
   ```
   Helper: extract each metric by `ParameterName` from the returned list before building the string.

7. `src/LeanAI.Application/WeightManagement/Commands/AppendActivityComment/AppendActivityCommentCommand.cs`
   - `sealed record AppendActivityCommentCommand(DateOnly Date, string Comment) : IRequest`

8. `src/LeanAI.Application/WeightManagement/Commands/AppendActivityComment/AppendActivityCommentCommandHandler.cs`
   - Injects: `IDailyActualWeightRepository`
   - Logic:
     1. `entry = await repo.GetByDateAsync(request.Date, ct)`
     2. If `entry` is not null: `entry.Notes = string.IsNullOrEmpty(entry.Notes) ? request.Comment : entry.Notes + "\n" + request.Comment`; call `repo.UpsertAsync(entry, ct)`
     3. If `entry` is null: create `new DailyActualWeight { Date = request.Date, WeightKg = 0, Notes = request.Comment }`; call `repo.UpsertAsync(newEntry, ct)`

---

### Step 3 — Infrastructure Layer

**Files to create:**

9. `src/LeanAI.Infrastructure/ActivityTracking/Repositories/ActivityLogRepository.cs`
   - Implements `IActivityLogRepository`
   - `AddRangeAsync`: `context.ActivityLogs.AddRange(logs); await context.SaveChangesAsync(ct)`

10. `src/LeanAI.Infrastructure/ActivityTracking/Services/GeminiRunImageAnalysisService.cs`
    - Implements `IRunImageAnalysisService`
    - Injects `IChatClient`
    - System instruction: instructs Gemini to respond with a JSON array only (no markdown fences), using the three `parameter_name` values: `distance`, `pace`, `duration`
    - Builds `ChatMessage` list:
      - `ChatRole.System` → system instruction string
      - `ChatRole.User` → `AIContent[]` containing `DataContent(imageBytes, mimeType)` + `TextContent("Extract the running metrics from this screenshot.")`
    - Calls `chatClient.GetResponseAsync(messages, ct)`
    - Strips markdown fences from response (reuse the same fence-stripping pattern from `GeminiGoalValidationService`)
    - Deserialises as `List<GeminiMetricDto>` (internal record with `parameter_name`, `value`, `unit`)
    - Validates: list must be non-null, non-empty, and contain entries for `distance`, `pace`, and `duration`
    - Throws `InvalidOperationException("Could not extract run metrics from the provided image.")` if validation fails
    - Returns `IReadOnlyList<ActivityMetricDto>` mapped from the parsed list

**Files to modify:**

11. `src/LeanAI.Infrastructure/Persistence/LeanAIDbContext.cs`
    - Add `public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();`
    - Add `modelBuilder.Entity<ActivityLog>` configuration in `OnModelCreating`:
      - PK: `e.Id`
      - Ignore `e.DomainEvents`
      - `Date` with `HasConversion` (DateOnly ↔ `"yyyy-MM-dd"` string) — same pattern as other entities
      - `Activity`, `ParameterName`, `Value`, `Unit` all `IsRequired()`

12. `src/LeanAI.Infrastructure/DependencyInjection.cs`
    - Register `IActivityLogRepository → ActivityLogRepository` (scoped)
    - Register `IRunImageAnalysisService → GeminiRunImageAnalysisService` (transient)

**EF Core Migration:**

13. Run: `dotnet ef migrations add Add_ActivityLogs --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Maui`
    - Creates `ActivityLogs` table with columns: `Id` (TEXT PK), `Date` (TEXT NOT NULL), `Activity` (TEXT NOT NULL), `ParameterName` (TEXT NOT NULL), `Value` (TEXT NOT NULL), `Unit` (TEXT NOT NULL)
    - No existing tables are altered

---

### Step 4 — Presentation Layer: Android Activity

**Files to create:**

14. `src/LeanAI.Maui/Platforms/Android/ImportRunActivity.cs`
    - Class: `public class ImportRunActivity : Activity`
    - Attributes:
      ```csharp
      [Activity(Label = "LeanAI: Import Run", Exported = true)]
      [IntentFilter(
          new[] { Intent.ActionSend },
          Categories = new[] { Intent.CategoryDefault },
          DataMimeType = "image/*",
          Label = "LeanAI: Import Run")]
      ```
    - `OnCreate` logic:
      1. Call `base.OnCreate(savedInstanceState)`
      2. Read `imageUri` from `Intent?.GetParcelableExtra<Android.Net.Uri>(Intent.ExtraStream)`; if null → `Finish()` and return
      3. Read MIME type via `ContentResolver!.GetType(imageUri) ?? "image/jpeg"`
      4. Read image bytes: open `ContentResolver.OpenInputStream(imageUri)`, copy to `MemoryStream`, close stream
      5. Resolve `IMediator` via `IPlatformApplication.Current!.Services.GetRequiredService<IMediator>()`
      6. `Task.Run(async () => { ... })`:
         - `try`: `var metrics = await mediator.Send(new ImportRunCommand(bytes, mimeType, DateOnly.FromDateTime(DateTime.Today)))`
           - Extract distance and duration from `metrics` for the toast message
           - `RunOnUiThread(() => Toast.MakeText(this, $"Run imported: {distVal} {distUnit} in {durVal}.", ToastLength.Short)!.Show())`
         - `catch`: `RunOnUiThread(() => Toast.MakeText(this, "Could not extract run data — please try again.", ToastLength.Long)!.Show())`
         - `finally`: `RunOnUiThread(Finish)`

    **Note:** No changes to `AndroidManifest.xml` are needed — the `[IntentFilter]` attribute generates the manifest entry automatically via MAUI's Android build tooling.

---

### Step 5 — Tests

**Files to create in `LeanAI.Tests`:**

15. `ActivityTracking/Commands/ImportRun/ImportRunCommandHandlerTests.cs`
    - **Branch: success path** — mock `IRunImageAnalysisService` returns 3 metrics → verifies `IActivityLogRepository.AddRangeAsync` called with 3 rows with correct field values → verifies `IMediator.Send` called with `AppendActivityCommentCommand` carrying the correct date and comment text → verifies handler returns the metrics list
    - **Branch: service throws** — mock `IRunImageAnalysisService` throws `InvalidOperationException` → verifies exception propagates (no DB or mediator calls)

16. `WeightManagement/Commands/AppendActivityComment/AppendActivityCommentCommandHandlerTests.cs`
    - **Branch: entry exists, notes empty** — `GetByDateAsync` returns entry with `Notes = null` → verifies `UpsertAsync` called with `Notes = comment`
    - **Branch: entry exists, notes not empty** — `GetByDateAsync` returns entry with `Notes = "existing"` → verifies `UpsertAsync` called with `Notes = "existing\n{comment}"`
    - **Branch: no entry** — `GetByDateAsync` returns null → verifies `UpsertAsync` called with new entry `WeightKg = 0`, `Notes = comment`

17. `ActivityTracking/Services/GeminiRunImageAnalysisServiceTests.cs`
    - **Branch: valid JSON array** — `ParseResponse` returns 3 `ActivityMetricDto` with correct field mapping
    - **Branch: JSON with markdown fences** — fences are stripped; parsing succeeds
    - **Branch: empty array** — throws `InvalidOperationException`
    - **Branch: missing required parameter** (e.g., only `distance` and `pace`, no `duration`) — throws `InvalidOperationException`
    - **Branch: malformed JSON** — throws `InvalidOperationException`

---

## File Change Summary

| File | Action |
|------|--------|
| `Domain/ActivityTracking/Entities/ActivityLog.cs` | CREATE |
| `Domain/ActivityTracking/Interfaces/IActivityLogRepository.cs` | CREATE |
| `Application/ActivityTracking/DTOs/ActivityMetricDto.cs` | CREATE |
| `Application/ActivityTracking/Services/IRunImageAnalysisService.cs` | CREATE |
| `Application/ActivityTracking/Commands/ImportRun/ImportRunCommand.cs` | CREATE |
| `Application/ActivityTracking/Commands/ImportRun/ImportRunCommandHandler.cs` | CREATE |
| `Application/WeightManagement/Commands/AppendActivityComment/AppendActivityCommentCommand.cs` | CREATE |
| `Application/WeightManagement/Commands/AppendActivityComment/AppendActivityCommentCommandHandler.cs` | CREATE |
| `Infrastructure/ActivityTracking/Repositories/ActivityLogRepository.cs` | CREATE |
| `Infrastructure/ActivityTracking/Services/GeminiRunImageAnalysisService.cs` | CREATE |
| `Infrastructure/Persistence/LeanAIDbContext.cs` | MODIFY — add `ActivityLogs` DbSet + model config |
| `Infrastructure/DependencyInjection.cs` | MODIFY — register 2 new services |
| `Infrastructure/Migrations/Add_ActivityLogs.cs` | GENERATE via `dotnet ef migrations add` |
| `Maui/Platforms/Android/ImportRunActivity.cs` | CREATE |
| `Tests/ActivityTracking/Commands/ImportRunCommandHandlerTests.cs` | CREATE |
| `Tests/WeightManagement/Commands/AppendActivityCommentCommandHandlerTests.cs` | CREATE |
| `Tests/ActivityTracking/Services/GeminiRunImageAnalysisServiceTests.cs` | CREATE |

**No existing tests are modified. No existing tables are altered.**

---

## Definition of Done Checklist

- [ ] Android Share Sheet lists "LeanAI: Import Run" when sharing an image from another app
- [ ] Valid run screenshot → 3 `ActivityLog` rows in DB (distance, pace, duration)
- [ ] Today's `DailyActualWeight.Notes` updated with formatted run summary (entry created with `WeightKg=0` if none existed)
- [ ] Success Toast shown with distance and duration values
- [ ] Invalid image → failure Toast shown
- [ ] `ImportRunCommandHandler` at 100% branch coverage
- [ ] `AppendActivityCommentCommandHandler` at 100% branch coverage
- [ ] `GeminiRunImageAnalysisService` parse logic fully tested (valid, fenced, empty, incomplete, malformed)
- [ ] EF Core migration `Add_ActivityLogs` applies cleanly; no existing tables altered
- [ ] `dotnet build` → 0 errors, 0 warnings
- [ ] `dotnet test` → all tests green
