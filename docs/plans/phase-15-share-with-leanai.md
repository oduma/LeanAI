# Phase 15: Unified "Share with LeanAI" Entry Point

## Goal
Replace the two separate Android share targets ("Import Food" and "Import Run") with a single "Share with LeanAI" entry. Gemini classifies the shared image — food or run — then routes to the existing `FoodReviewPage` or `RunReviewPage` review flows unchanged. Unknown images show an error toast.

## Key design decisions
- **Two-step AI:** Step 1 is a fast classification call (returns `food | run | unknown`). Step 2 is the existing `AnalyzeFoodImageCommand` or `AnalyzeRunImageCommand` — no changes to those handlers.
- **Unknown → silent error:** Toast + `Finish()`, no data saved, no dialog.
- **App-agnostic run detection:** The existing `GeminiRunImageAnalysisService` system prompt is already generic; no changes needed.
- **Domain home for cross-cutting classification:** New `LeanAI.Domain.Shared` namespace. Keeps classification outside both `FoodTracking` and `ActivityTracking` bounded contexts.

---

## Implementation Steps

### Step 1 — Add `ShareImageType` enum to Domain

**New file:** `src/LeanAI.Domain/Shared/ShareImageType.cs`

```csharp
namespace LeanAI.Domain.Shared;

public enum ShareImageType { Food, Run, Unknown }
```

---

### Step 2 — Add `IShareImageClassificationService` to Domain

**New file:** `src/LeanAI.Domain/Shared/Interfaces/IShareImageClassificationService.cs`

```csharp
namespace LeanAI.Domain.Shared.Interfaces;

public interface IShareImageClassificationService
{
    Task<ShareImageType> ClassifyAsync(
        byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
```

---

### Step 3 — Add `ClassifyShareImageCommand` to Application layer

**New folder:** `src/LeanAI.Application/Shared/Commands/ClassifyShareImage/`

**`ClassifyShareImageCommand.cs`:**
```csharp
namespace LeanAI.Application.Shared.Commands.ClassifyShareImage;

public record ClassifyShareImageCommand(byte[] ImageBytes, string MimeType)
    : IRequest<ShareImageType>;
```

**`ClassifyShareImageCommandHandler.cs`:**
```csharp
namespace LeanAI.Application.Shared.Commands.ClassifyShareImage;

public sealed class ClassifyShareImageCommandHandler(
    IShareImageClassificationService classifier)
    : IRequestHandler<ClassifyShareImageCommand, ShareImageType>
{
    public Task<ShareImageType> Handle(
        ClassifyShareImageCommand request, CancellationToken ct)
        => classifier.ClassifyAsync(request.ImageBytes, request.MimeType, ct);
}
```

This handler is intentionally thin — all logic lives in the infrastructure service so it can be tested independently.

---

### Step 4 — Add `GeminiShareImageClassificationService` to Infrastructure

**New file:** `src/LeanAI.Infrastructure/Shared/Services/GeminiShareImageClassificationService.cs`

**Classification prompt (system instruction):**
```
You are an image classifier for a fitness tracking app.
Classify the image into exactly one category:
- "food" — the image shows food, a meal, ingredients, or a restaurant menu.
- "run" — the image is a screenshot from a fitness or running app showing metrics such as distance, pace, duration, or calories burned.
- "unknown" — the image is neither of the above.
Respond with exactly one lowercase word: food, run, or unknown. No other text.
```

**Response parsing:** Trim whitespace; switch on `"food"`, `"run"`, `"unknown"`. Any other response → `Unknown`. Exceptions → `Unknown` (caller shows toast).

Implements `IShareImageClassificationService`.

---

### Step 5 — Register `GeminiShareImageClassificationService` in DI

**File:** `src/LeanAI.Infrastructure/DependencyInjection.cs`

Add one line in the Gemini services block:
```csharp
services.AddTransient<IShareImageClassificationService, GeminiShareImageClassificationService>();
```

Also add the using:
```csharp
using LeanAI.Domain.Shared.Interfaces;
using LeanAI.Infrastructure.Shared.Services;
```

---

### Step 6 — Add `ShareWithLeanAIActivity`

**New file:** `src/LeanAI.Maui/Platforms/Android/ShareWithLeanAIActivity.cs`

**Intent filter attributes:**
```csharp
[Activity(Label = "Share with LeanAI", Exported = true)]
[IntentFilter(
    new[] { global::Android.Content.Intent.ActionSend },
    Categories = new[] { global::Android.Content.Intent.CategoryDefault },
    DataMimeType = "image/*",
    Label = "Share with LeanAI")]
```

**`OnCreate` flow:**
1. `SetLoadingView()` — same design language as the existing activities (dark background, LeanAI title, copper spinner, Nickel "Analysing your image…" label)
2. Extract `imageUri` from `Intent.ExtraStream`; if null → `Finish()` and return
3. Read image bytes via `ContentResolver`
4. In `Task.Run(async () => { ... })`:
   a. Load API key from `SecureStorage` → update `GeminiKeyHolder`
   b. `var type = await mediator.Send(new ClassifyShareImageCommand(bytes, mimeType))`
   c. Switch on `type`:
      - `Food` → `var items = await mediator.Send(new AnalyzeFoodImageCommand(bytes, mimeType, DateOnly.Today))` → `foodImportState.Set(items)` → `StartActivity(mainIntent)` → `RunOnUiThread(Finish)`
      - `Run` → `var result = await mediator.Send(new AnalyzeRunImageCommand(bytes, mimeType))` → build `RunActivityRowDto` → `runImportState.Set([row])` → `StartActivity(mainIntent)` → `RunOnUiThread(Finish)`
      - `Unknown` → `RunOnUiThread(() => { Toast("Couldn't identify this image. Share a food photo or a run screenshot."); Finish(); })`
5. Outer `catch` → `RunOnUiThread(() => { Toast("Could not process the image — please try again."); Finish(); })`

---

### Step 7 — Delete old Activities

Delete both files:
- `src/LeanAI.Maui/Platforms/Android/ImportFoodActivity.cs`
- `src/LeanAI.Maui/Platforms/Android/ImportRunActivity.cs`

These are entirely superseded by `ShareWithLeanAIActivity`. No other files reference them.

---

### Step 8 — Write unit tests

**New file:** `tests/LeanAI.Tests/Application/Shared/Commands/ClassifyShareImageCommandHandlerTests.cs`

Three tests covering the handler:

| Test name | Setup | Expected result |
|---|---|---|
| `Handle_FoodImage_ReturnsFoodType` | Mock returns `Food` | Handler returns `Food` |
| `Handle_RunImage_ReturnsRunType` | Mock returns `Run` | Handler returns `Run` |
| `Handle_UnknownImage_ReturnsUnknownType` | Mock returns `Unknown` | Handler returns `Unknown` |

Pattern follows existing handler tests (Moq + FluentAssertions). The handler is thin, so these tests verify correct delegation to the service.

---

### Step 9 — Build and verify

```
dotnet build src/LeanAI.Maui/LeanAI.Maui.csproj -c Release  → 0 errors
dotnet test                                                   → all tests green
```

Manual on-device verification (Pixel 10):
- Share a food photo → "Share with LeanAI" appears once in share sheet → `FoodReviewPage` opens with items
- Share a run screenshot → `RunReviewPage` opens with metrics
- Share a beach photo → toast shown, app does not open

---

## Files changed (summary)

| File | Action |
|---|---|
| `src/LeanAI.Application/Shared/ShareImageType.cs` | **Add** (moved from Domain — see corrections) |
| `src/LeanAI.Application/Shared/Services/IShareImageClassificationService.cs` | **Add** (moved from Domain — see corrections) |
| `src/LeanAI.Application/Shared/Commands/ClassifyShareImage/ClassifyShareImageCommand.cs` | **Add** |
| `src/LeanAI.Application/Shared/Commands/ClassifyShareImage/ClassifyShareImageCommandHandler.cs` | **Add** |
| `src/LeanAI.Infrastructure/Shared/Services/GeminiShareImageClassificationService.cs` | **Add** |
| `src/LeanAI.Infrastructure/DependencyInjection.cs` | **Edit** — add DI registration |
| `src/LeanAI.Maui/Platforms/Android/ShareWithLeanAIActivity.cs` | **Add** |
| `src/LeanAI.Maui/Platforms/Android/ImportFoodActivity.cs` | **Delete** |
| `src/LeanAI.Maui/Platforms/Android/ImportRunActivity.cs` | **Delete** |
| `tests/LeanAI.Tests/Application/Shared/Commands/ClassifyShareImageCommandHandlerTests.cs` | **Add** |
| `tests/LeanAI.Tests/Infrastructure/Shared/GeminiShareImageClassificationServiceTests.cs` | **Add** (not in plan — see corrections) |
| `src/LeanAI.Maui/ViewModels/LogViewModel.cs` | **Edit** — pending check before LoadCoreAsync (not in plan — see corrections) |

---

## Definition of Done (DoD)

- [x] `LeanAI.Application.Shared.ShareImageType` enum exists
- [x] `IShareImageClassificationService` interface in Application
- [x] `ClassifyShareImageCommand` + handler in Application
- [x] `GeminiShareImageClassificationService` in Infrastructure with classification prompt
- [x] DI registration added
- [x] `ShareWithLeanAIActivity` with correct intent filter label "Share with LeanAI"
- [x] `ImportFoodActivity` and `ImportRunActivity` deleted
- [x] 16 unit tests added (3 handler + 10 parse theory + 3 others)
- [x] `dotnet build` → 0 errors
- [x] `dotnet test` → 225 / 225 green
- [x] Android share sheet shows exactly one LeanAI entry
- [x] Food photo → `FoodReviewPage` with pre-populated items
- [x] Run screenshot → `RunReviewPage` with pre-populated metrics
- [x] Unknown image → toast shown, activity closes
- [x] Review modal appears immediately — log screen never interactive before modal

## Corrections (post-implementation)

- **Service interfaces moved to Application layer.** Plan placed `IShareImageClassificationService` and `ShareImageType` in `LeanAI.Domain.Shared`. The existing codebase pattern (all AI service interfaces in Application) required moving both to `LeanAI.Application.Shared`.
- **Additional Infrastructure parse tests.** `GeminiShareImageClassificationServiceTests` (10 theory tests) was added to cover `ParseResponse` edge cases — same pattern used by `GeminiRunImageAnalysisService` tests.
- **`LogViewModel.LoadLogAsync` reordered.** After deployment, the log screen loaded and became interactive before the review modal appeared. Fixed by checking `HasPending` first and firing `LoadCoreAsync` in the background so the review modal is pushed immediately without waiting for SQLite queries to complete.

## Status: ✅ COMPLETE (2026-06-04)
