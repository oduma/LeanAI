# Phase 2: AI Safety Validation (The Guardrails) — Implementation Plan

> **Status: COMPLETE** — Delivered 2026-05-10

**Goal:** Validate the user's weight-loss goal against Gemini 2.5 Flash at the end of Step 3 of the Setup Wizard. Display the AI's medical assessment below the goal fields. Block progression (keep Done disabled) if the result is Warning or Danger.  
**DoD:** Safe → Done enabled. Warning → amber icon + AI text + Done disabled. Danger → red icon + AI text + Done disabled. Changing fields re-validates. 0 build errors/warnings. All tests green.

---

## Decisions & Constraints

| Decision | Choice |
|---|---|
| Trigger | Auto-fire when both `TargetWeightText` and `TargetPeriod` are set; 500 ms debounce; re-fires on every change. |
| Colors | Add two new colors: Amber `#E8A838` for Warning, Danger Red `#C0392B` for Danger. Copper `#D28B5C` stays exclusively for SUCCESS. |
| Override | Permanently blocked. Done remains disabled for both Warning and Danger. User must adjust their goal until AI returns Safe. |
| AI response format | Prompt instructs Gemini to reply with JSON only: `{"status":"SAFE"\|"WARNING"\|"DANGER","message":"…"}`. Baked-in classification eliminates secondary parse step. |
| Gemini SDK | `Mscc.GenerativeAI.Microsoft` NuGet — provides `IChatClient` adapter over the Gemini HTTP API. Works with `Microsoft.Extensions.AI` (already in Infrastructure). |
| Model | `gemini-2.5-flash` |
| API key flow | `App.cs.ShowWizardIfNeededAsync()` provisions the key into `SecureStorage` (Android Keystore) on first launch, then reads it on every subsequent launch. A `GeminiKeyHolder` singleton (Infrastructure-internal class, registered in DI) carries the key from `App.cs` to `GeminiGoalValidationService` without exposing SecureStorage to Infrastructure. See §5.5. |
| Why not file-read | The workspace path `z-com-ai/key.txt` does not exist on an Android device. File-read is not viable on-device. |
| Why not `MauiProgram.cs` sync | `SecureStorage` must be called after the MAUI platform is initialised (not safe in `CreateMauiApp`). `App.cs` runs post-initialisation. |
| `IAIGoalValidationService` layer | Application (not Domain) — external service interfaces that are not persistence contracts live in Application, not Domain. |
| `GoalValidationStatus` layer | Application — it is an application-level result, not a core domain concept. |
| Validation panel placement | Below the period selector buttons in the Step 3 `VerticalStackLayout`, inside the existing `ScrollView`. |
| Validation icons | Two new SVG files with baked-in fill colors: `icon_warning.svg` (amber `#E8A838`) and `icon_stop.svg` (danger red `#C0392B`). |
| No DB migration | Validation results are ephemeral — held only in `WizardViewModel`. Nothing persisted. |
| Loading state | `ActivityIndicator` (Copper) is visible while `IsValidating = true`; result panel visible when `ValidationResult` is not null. |
| `CanGoNext()` Step 3 | Requires `IsStep3Valid() && ValidationStatus == GoalValidationStatus.Safe`. |
| Clear on re-run | `PrepareForRerun()` clears `ValidationStatus` and `ValidationMessage` so the re-run starts with a blank validation panel. |

---

## Implementation Order (TDD: Red → Green → Refactor)

```
1. Application layer (interface + query + handler)
2. Application tests (red → green)
3. Infrastructure (GeminiGoalValidationService + DI wiring)
4. Presentation (WizardViewModel + WizardPage.xaml)
```

---

## 1. New NuGet Package

| Package | Project | Purpose |
|---|---|---|
| `Mscc.GenerativeAI.Microsoft` | `LeanAI.Infrastructure` | `IChatClient` adapter for Gemini API, compatible with `Microsoft.Extensions.AI` |

---

## 2. Application Layer (`LeanAI.Application`)

### `WeightManagement/Enums/GoalValidationStatus.cs`

```csharp
public enum GoalValidationStatus { Safe, Warning, Danger }
```

### `WeightManagement/DTOs/GoalValidationResult.cs`

```csharp
public record GoalValidationResult(GoalValidationStatus Status, string Message);
```

### `WeightManagement/Services/IAIGoalValidationService.cs`

```csharp
public interface IAIGoalValidationService
{
    Task<GoalValidationResult> ValidateAsync(ValidateGoalQuery query, CancellationToken ct = default);
}
```

### `WeightManagement/Queries/ValidateGoal/ValidateGoalQuery.cs`

```csharp
public record ValidateGoalQuery(
    Gender Gender,
    int Age,
    double HeightCm,
    double StartingWeightKg,
    double TargetWeightKg,
    TargetPeriod TargetPeriod
) : IRequest<GoalValidationResult>;
```

### `WeightManagement/Queries/ValidateGoal/ValidateGoalQueryHandler.cs`

```csharp
public class ValidateGoalQueryHandler(IAIGoalValidationService validationService)
    : IRequestHandler<ValidateGoalQuery, GoalValidationResult>
{
    public Task<GoalValidationResult> Handle(ValidateGoalQuery request, CancellationToken cancellationToken)
        => validationService.ValidateAsync(request, cancellationToken);
}
```

---

## 3. Application Tests (`LeanAI.Tests/Application/WeightManagement/`)

### `ValidateGoalQueryHandlerTests.cs` — 100% branch coverage (Moq)

| Test | Scenario |
|---|---|
| `Handle_WhenServiceReturnsSafe_ReturnsSafeResult` | Mock returns `Safe` result → handler returns it unchanged |
| `Handle_WhenServiceReturnsWarning_ReturnsWarningResult` | Mock returns `Warning` result → handler returns it unchanged |
| `Handle_WhenServiceReturnsDanger_ReturnsDangerResult` | Mock returns `Danger` result → handler returns it unchanged |
| `Handle_WhenServiceThrows_PropagatesException` | Mock throws `InvalidOperationException` → handler does not swallow it |

---

## 4. Infrastructure Layer (`LeanAI.Infrastructure`)

### `Services/GeminiGoalValidationService.cs`

Constructor receives `IChatClient chatClient`.  
Implements `IAIGoalValidationService`.

**Prompt template** (system role or first user message):

```
You are a trusted medical advisor and fitness specialist evaluating weight-loss goals for medical safety.

Reply with valid JSON only — no markdown fences, no extra text:
{"status":"SAFE"|"WARNING"|"DANGER","message":"Your plain-text assessment (2–3 sentences max)."}

Status rules:
- SAFE: Goal is medically sound, achievable, and within recognised safe weight-loss guidelines.
- WARNING: Goal is aggressive; achievable only with extreme effort; carries meaningful health risk.
- DANGER: Goal is medically unsafe or physically impossible in the given timeframe.

Profile: {Gender}, {Age} years old, {Height}, current weight {StartWeight}.
Goal: Reach {TargetWeight} in {Period}.
```

**Parsing** (`ParseResponse` internal method):
1. Strip leading/trailing whitespace and any ` ```json ` / ` ``` ` fences.
2. `JsonSerializer.Deserialize` into a private `GeminiJsonResponse { string Status; string Message }` record.
3. Map `"SAFE"` → `GoalValidationStatus.Safe`, `"WARNING"` → `Warning`, `"DANGER"` → `Danger`; unknown → `Danger` (fail-safe).
4. Return `GoalValidationResult(status, message)`.

### `GeminiKeyHolder.cs` (new, internal to Infrastructure)

```csharp
internal sealed class GeminiKeyHolder
{
    public string ApiKey { get; set; } = string.Empty;
}
```

Registered as a singleton. `App.cs` sets `ApiKey` after reading from `SecureStorage`. `GeminiGoalValidationService` reads it when first resolving `IChatClient`. Because `IChatClient` is also a singleton resolved lazily (only when the first validation fires on Step 3 — well after app startup), the key is always populated by the time the factory runs.

### Public extension method (add to `DependencyInjection.cs`)

```csharp
public static void SetGeminiApiKey(this IServiceProvider services, string apiKey)
    => services.GetRequiredService<GeminiKeyHolder>().ApiKey = apiKey;
```

Called from `App.cs` after SecureStorage read completes.

### `DependencyInjection.cs` (update)

Signature stays `AddInfrastructure(this IServiceCollection services, string dbPath)` — no key parameter needed at registration time.

```csharp
var keyHolder = new GeminiKeyHolder();
services.AddSingleton(keyHolder);

services.AddSingleton<IChatClient>(sp =>
{
    var holder = sp.GetRequiredService<GeminiKeyHolder>();
    // Factory runs lazily on first resolution (first Step 3 validation)
    return new GoogleGenerativeAIClient(holder.ApiKey, "gemini-2.5-flash");
});

services.AddTransient<IAIGoalValidationService, GeminiGoalValidationService>();
```

*(Exact `GoogleGenerativeAIClient` constructor confirmed during implementation — `Mscc.GenerativeAI.Microsoft` may expose builder extension methods.)*

---

## 5. Presentation Layer (`LeanAI.Maui`)

### 5.1 New Color Resources — `Resources/Styles/Colors.xaml`

Add inside the `<ResourceDictionary>`:

```xml
<Color x:Key="ColorAmber">#E8A838</Color>
<Color x:Key="ColorDanger">#C0392B</Color>
```

### 5.2 New SVG Assets — `Resources/Images/`

| File | Shape | Fill Color |
|---|---|---|
| `icon_warning.svg` | Warning triangle with exclamation (Material Design `warning` path, 24×24) | `#E8A838` |
| `icon_stop.svg` | Filled circle with horizontal bar (Material Design `block` / `cancel` path, 24×24) | `#C0392B` |

Bake the fill color directly into the SVG `fill` attribute — unlike tab icons these are content-area images that MAUI does not tint at runtime.

### 5.3 `ViewModels/WizardViewModel.cs` (update)

**New constructor dependency:** add `IMediator` already present — no change needed.

**New observable properties:**

```csharp
[ObservableProperty]
private bool _isValidating;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ValidationIconSource))]
[NotifyPropertyChangedFor(nameof(ValidationTextColor))]
[NotifyPropertyChangedFor(nameof(HasValidationResult))]
[NotifyCanExecuteChangedFor(nameof(NextCommand))]
private GoalValidationStatus? _validationStatus;

[ObservableProperty]
private string _validationMessage = string.Empty;

public bool HasValidationResult => ValidationStatus.HasValue;

public string ValidationIconSource => ValidationStatus switch
{
    GoalValidationStatus.Warning => "icon_warning.svg",
    GoalValidationStatus.Danger  => "icon_stop.svg",
    _                            => string.Empty
};

public Color ValidationTextColor => ValidationStatus switch
{
    GoalValidationStatus.Warning => Color.FromArgb("#E8A838"),
    GoalValidationStatus.Danger  => Color.FromArgb("#C0392B"),
    _                            => Colors.Transparent
};
```

**Updated `CanGoNext()` — Step 3 arm:**

```csharp
3 => IsStep3Valid() && ValidationStatus == GoalValidationStatus.Safe,
```

**Partial property-changed hooks (source-generator partial methods):**

```csharp
partial void OnTargetWeightTextChanged(string value) => TriggerValidationDebounce();
partial void OnTargetPeriodChanged(TargetPeriod? value) => TriggerValidationDebounce();
```

**`TriggerValidationDebounce()` method:**

```csharp
private CancellationTokenSource? _validationCts;

private void TriggerValidationDebounce()
{
    if (CurrentStep != 3) return;

    _validationCts?.Cancel();
    ValidationStatus  = null;
    ValidationMessage = string.Empty;

    if (!IsStep3Valid()) return;

    _validationCts = new CancellationTokenSource();
    var token = _validationCts.Token;

    Task.Delay(500, token).ContinueWith(
        t => { if (!t.IsCanceled) MainThread.BeginInvokeOnMainThread(() => _ = ValidateGoalAsync()); },
        TaskScheduler.Default);
}
```

**`ValidateGoalAsync()` method:**

```csharp
private async Task ValidateGoalAsync()
{
    if (!IsStep3Valid()) return;

    _ = int.TryParse(AgeText, out var age);
    var heightCm        = ParseHeightCm();
    var startingWeightKg = ParseWeightToKg(StartingWeightText);
    var targetWeightKg   = ParseWeightToKg(TargetWeightText);

    if (!Gender.HasValue || age <= 0 || !heightCm.HasValue ||
        !startingWeightKg.HasValue || !targetWeightKg.HasValue || !TargetPeriod.HasValue)
        return;

    IsValidating = true;
    try
    {
        var result = await _mediator.Send(new ValidateGoalQuery(
            Gender.Value, age, heightCm.Value,
            startingWeightKg.Value, targetWeightKg.Value, TargetPeriod.Value));

        ValidationStatus  = result.Status;
        ValidationMessage = result.Message;
    }
    finally
    {
        IsValidating = false;
    }
}
```

**`PrepareForRerun()` update** — add two lines:

```csharp
ValidationStatus  = null;
ValidationMessage = string.Empty;
```

### 5.4 `Views/Wizard/WizardPage.xaml` (update)

Add validation panel inside the Step 3 `VerticalStackLayout`, below the period button row:

```xml
<!-- AI goal validation panel -->
<ActivityIndicator
    IsVisible="{Binding IsValidating}"
    IsRunning="{Binding IsValidating}"
    Color="{StaticResource ColorCopper}"
    HorizontalOptions="Center" />

<VerticalStackLayout
    IsVisible="{Binding HasValidationResult}"
    Spacing="8"
    Margin="0,8,0,0">
    <HorizontalStackLayout Spacing="10" VerticalOptions="Center">
        <Image
            Source="{Binding ValidationIconSource}"
            WidthRequest="22"
            HeightRequest="22"
            VerticalOptions="Center" />
        <Label
            Text="{Binding ValidationMessage}"
            TextColor="{Binding ValidationTextColor}"
            FontSize="13"
            LineBreakMode="WordWrap"
            HorizontalOptions="FillAndExpand" />
    </HorizontalStackLayout>
</VerticalStackLayout>
```

> **Note:** `TextColor` bound to a `Color` ViewModel property is supported in MAUI — no converter needed.

### 5.5 `App.xaml.cs` (update)

Add `ProvisionGeminiKeyAsync()` and call it inside `ShowWizardIfNeededAsync()` (or directly from the `window.Created` handler before the wizard check):

```csharp
private const string GeminiKeyStorageKey = "gemini_key";
// Bootstrap value — stored in SecureStorage on first launch only.
// On subsequent launches the SecureStorage value is used and this constant is not read.
private const string GeminiBootstrapKey  = "AIzaSyB89Doqu3Pekm74qqxjjVyxGR8NdUfgTEA";

protected override Window CreateWindow(IActivationState? activationState)
{
    var window = new Window(new AppShell());
    window.Created += async (_, _) =>
    {
        await ProvisionGeminiKeyAsync();
        await ShowWizardIfNeededAsync();
    };
    return window;
}

private async Task ProvisionGeminiKeyAsync()
{
    var key = await SecureStorage.GetAsync(GeminiKeyStorageKey);
    if (string.IsNullOrEmpty(key))
    {
        key = GeminiBootstrapKey;
        await SecureStorage.SetAsync(GeminiKeyStorageKey, key);
    }
    _services.SetGeminiApiKey(key);   // extension on IServiceProvider (see §4)
}
```

> **Why this works:** `window.Created` runs after MAUI platform initialisation, so `SecureStorage` is available. The `IChatClient` singleton is resolved lazily — only when the first Step 3 validation fires — which is always after `ProvisionGeminiKeyAsync` has completed.

> **Security:** On Android, `SecureStorage` is backed by the Android Keystore system. The bootstrap key constant is compiled into the binary (unavoidable for a pre-provisioned key), but from the second launch onwards the key is read only from Keystore-encrypted storage — never from a plaintext file or config.

### 5.6 `MauiProgram.cs` (update — minimal)

Call `AddInfrastructure` with only `dbPath` (no key parameter — key is provisioned by `App.cs` at runtime):

```csharp
builder.Services.AddInfrastructure(dbPath);
```

---

## 6. File Additions Summary

```
New files:
  src/LeanAI.Application/WeightManagement/Enums/
    GoalValidationStatus.cs

  src/LeanAI.Application/WeightManagement/DTOs/
    GoalValidationResult.cs

  src/LeanAI.Application/WeightManagement/Services/
    IAIGoalValidationService.cs

  src/LeanAI.Application/WeightManagement/Queries/ValidateGoal/
    ValidateGoalQuery.cs
    ValidateGoalQueryHandler.cs

  src/LeanAI.Infrastructure/Services/
    GeminiGoalValidationService.cs

  src/LeanAI.Maui/Resources/Images/
    icon_warning.svg                    ← amber warning triangle
    icon_stop.svg                       ← red danger circle

  tests/LeanAI.Tests/Application/WeightManagement/
    ValidateGoalQueryHandlerTests.cs

Modified files:
  src/LeanAI.Infrastructure/LeanAI.Infrastructure.csproj
    └─ +Mscc.GenerativeAI.Microsoft

  src/LeanAI.Infrastructure/DependencyInjection.cs
    └─ +GeminiKeyHolder singleton; +IChatClient lazy factory; +GeminiGoalValidationService; +SetGeminiApiKey() extension

  src/LeanAI.Maui/Resources/Styles/Colors.xaml
    └─ +ColorAmber (#E8A838), +ColorDanger (#C0392B)

  src/LeanAI.Maui/ViewModels/WizardViewModel.cs
    └─ +IsValidating, +ValidationStatus, +ValidationMessage, +HasValidationResult,
       +ValidationIconSource, +ValidationTextColor, +TriggerValidationDebounce(),
       +ValidateGoalAsync(); updated CanGoNext() Step 3; updated PrepareForRerun()

  src/LeanAI.Maui/Views/Wizard/WizardPage.xaml
    └─ +validation panel (ActivityIndicator + icon/message row) in Step 3

  src/LeanAI.Maui/App.xaml.cs
    └─ +ProvisionGeminiKeyAsync() — SecureStorage read/write on first launch; calls SetGeminiApiKey()
  src/LeanAI.Maui/MauiProgram.cs
    └─ AddInfrastructure(dbPath) only — key provisioned by App.cs at runtime
```

---

## 7. Delivery Notes

| Item | Outcome |
|---|---|
| `ChatResponse.Message` does not exist | The plan referenced `response.Message.Text`. In `Microsoft.Extensions.AI.Abstractions 10.3.0`, `ChatResponse` exposes `Text` (convenience property over all messages) and `Messages`, but not `Message`. Changed to `response.Text ?? string.Empty`. |
| `LayoutOptions.FillAndExpand` obsolete | Using `HorizontalOptions="FillAndExpand"` on the message `Label` inside a `HorizontalStackLayout` produced a CS0618 warning. Replaced the layout with a `Grid` (`ColumnDefinitions="Auto,*"`) which is the correct MAUI 10 approach for filling remaining space, and achieves proper word-wrap. |
| AI disclaimer label added post-plan | After Phase 2 was confirmed working, a Nickel italic disclaimer label ("This response was generated by AI. Please consult a qualified medical professional…") was added below the AI message. It is controlled by `ShowAiDisclaimer` — visible only when status is `Warning` or `Danger`; hidden for `Safe`. |
| `ShowAiDisclaimer` computed property | Added to `WizardViewModel` alongside the other validation properties. Decorated with `[NotifyPropertyChangedFor]` on `_validationStatus` so it updates whenever the status changes. |
| Gemini client construction | Plan showed `new GoogleGenerativeAIClient(apiKey, model)`. Actual `Mscc.GenerativeAI.Microsoft` 3.1.0 API uses `new GeminiClient(apiKey).AsIChatClient("gemini-2.5-flash")` via the `GeminiClientExtensions.AsIChatClient` extension method. |

---

## 8. Definition of Done Checklist

- [x] Goal validation triggers automatically (500 ms debounce) when both target weight and period are filled on Step 3
- [x] Activity indicator (Copper) shows while Gemini is responding
- [x] **Safe** result: Done button enabled; no icon or message shown; disclaimer hidden
- [x] **Warning** result: Amber warning icon + AI message shown; Done button remains disabled; disclaimer shown
- [x] **Danger** result: Red stop icon + AI message shown; Done button remains disabled; disclaimer shown
- [x] Changing either field clears the previous result and re-validates after 500 ms
- [x] `PrepareForRerun()` clears validation state; re-run Step 3 starts with a blank panel
- [x] Gemini API key provisioned into Android Keystore (`SecureStorage`) on first launch; read from Keystore on all subsequent launches
- [x] `ValidateGoalQueryHandlerTests` pass with 100% branch coverage (4 cases)
- [x] All pre-existing tests still pass (38/38 green)
- [x] `dotnet build` → 0 errors, 0 warnings
- [x] `dotnet test` → all tests green
