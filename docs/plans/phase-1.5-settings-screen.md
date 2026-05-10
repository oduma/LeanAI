# Phase 1.5: Settings Screen (The Control Panel) — Implementation Plan

> **Status: COMPLETE** — Delivered 2026-05-10

**Goal:** Implement the Settings tab with a functional gear icon (active/inactive), fix all three missing tab icons, add a "Re-Run the Setup" action with confirmation dialog and transactional profile restore-on-cancel.  
**DoD:** Settings gear is Copper when active / Nickel when inactive; re-run wizard resets profile; cancelling mid-wizard restores original profile; 0 build errors/warnings; all tests green.

---

## Decisions & Constraints

| Decision | Choice |
|---|---|
| Tab icon approach | Three monochrome SVGs (gear, pencil, chart) — MAUI Shell tints via `TabBarSelectedColor` (Copper `#D28B5C`) / `TabBarUnselectedColor` (Nickel `#9A9EAB`) applied on the `<Shell>` element. Applies to all three tabs uniformly. |
| Tab icon assets | Hand-crafted SVG markup. No external tooling. One file per tab (white silhouette, transparent bg). |
| Existing icon gap | `log_tab.png`, `trends_tab.png`, `settings_tab.png` referenced in AppShell do not exist. All three are replaced with new `.svg` files in this phase. |
| Confirmation dialog | `await Shell.Current.DisplayAlert()` called directly in `SettingsViewModel`. ViewModel lives in the MAUI project — MAUI dependency is acceptable there. |
| Transactional re-run | Snapshot profile in memory → delete from DB → launch wizard fresh. If wizard completes → snapshot discarded. If wizard cancelled → snapshot restored via `SaveUserProfileCommand`. |
| Wizard cancel signaling | `WizardDismissedMessage(bool Completed)` record sent via `WeakReferenceMessenger.Default`. Wizard sends it on Step 3 completion (`true`) and on re-run cancel from Step 1 (`false`). |
| Re-run mode on WizardViewModel | New `IsRerun` bool property. `PrepareForRerun()` public method: sets `IsRerun = true`, resets all fields to null/defaults, sets `CurrentStep = 1`. No `GetUserProfileQuery` called — wizard starts blank. |
| Back on Step 1 (re-run) | `Back()` command: when `IsRerun && CurrentStep == 1` → sends `WizardDismissedMessage(false)` + pops modal. In non-rerun mode (first launch), Back on Step 1 is a no-op (button hidden). |
| SettingsViewModel awaiting wizard | `TaskCompletionSource<bool>` inside `ReRunSetupAsync`. Registered messenger handler sets the result when `WizardDismissedMessage` arrives. `await tcs.Task` suspends after `PushModalAsync`, resumes when wizard is dismissed. |
| Post-wizard landing tab | No explicit navigation needed — wizard was pushed modally from Settings, so dismissing it returns naturally to Settings. |
| No DB migration | `DeleteAsync` operates on the existing `UserProfiles` table. No schema change. |

---

## Implementation Order (TDD: Red → Green → Refactor)

```
1. Domain interface  →  2. Application layer  →  3. Infrastructure  →  4. Presentation
```

Each non-Presentation layer: write failing tests first, then implement to pass.

---

## 1. Asset Preparation

Create three monochrome SVG files — white fill, transparent background, sized at 24×24 viewBox (standard tab icon). MAUI Resizetizer will scale for all densities.

| File | Icon Shape | Replaces |
|---|---|---|
| `Resources/Images/log_tab.svg` | Pencil / edit silhouette | `log_tab.png` (missing) |
| `Resources/Images/trends_tab.svg` | Line-chart / trend silhouette | `trends_tab.png` (missing) |
| `Resources/Images/settings_tab.svg` | Gear / cog silhouette | `settings_tab.png` (missing) |

All three go into `src/LeanAI.Maui/Resources/Images/`. The `AppShell.xaml` icon references are updated to the new `.svg` filenames.

---

## 2. Domain Layer (`LeanAI.Domain`)

### `Interfaces/IUserProfileRepository.cs`
Add one method to the existing interface:

```csharp
Task DeleteAsync(CancellationToken ct = default);
```

No domain tests added — this is a contract; it is tested indirectly through Application layer mocks.

---

## 3. Application Layer (`LeanAI.Application`)

### Folder: `WeightManagement/Commands/DeleteUserProfile/`

#### `DeleteUserProfileCommand.cs`
```csharp
public record DeleteUserProfileCommand : IRequest<Unit>;
```

#### `DeleteUserProfileCommandHandler.cs`
Implements `IRequestHandler<DeleteUserProfileCommand, Unit>`.  
Constructor: `IUserProfileRepository repository`.  
`Handle`: calls `repository.DeleteAsync(ct)`, returns `Unit.Value`.

---

## 4. Application Tests (`LeanAI.Tests/Application/WeightManagement/`)

**`DeleteUserProfileCommandHandlerTests.cs`** — 100% branch coverage (Moq):

| Test | Scenario |
|---|---|
| `Handle_WhenCalled_CallsDeleteAsync` | `DeleteAsync` is called exactly once on the mock repository |
| `Handle_WhenDeleteAsyncThrows_PropagatesException` | If `DeleteAsync` throws, the handler does not swallow the exception |

---

## 5. Infrastructure Layer (`LeanAI.Infrastructure`)

### `Repositories/UserProfileRepository.cs`
Implement `DeleteAsync`:

```csharp
public async Task DeleteAsync(CancellationToken ct = default)
{
    var profile = await _context.UserProfiles.FirstOrDefaultAsync(ct);
    if (profile is not null)
    {
        _context.UserProfiles.Remove(profile);
        await _context.SaveChangesAsync(ct);
    }
}
```

No migration required — operates on the existing `UserProfiles` table.

---

## 6. Presentation Layer (`LeanAI.Maui`)

### 6.1 Messages: `Messages/WizardDismissedMessage.cs`
New file in the MAUI project:

```csharp
namespace LeanAI.Maui.Messages;
public record WizardDismissedMessage(bool Completed);
```

### 6.2 `AppShell.xaml` (update)
Add tab bar color properties to the `<Shell>` element and update icon references:

```xml
<Shell
    ...
    Shell.TabBarSelectedColor="{StaticResource ColorCopper}"
    Shell.TabBarUnselectedColor="{StaticResource ColorNickel}"
    ...>
    <TabBar>
        <ShellContent Title="Log"      Icon="log_tab.svg"      ... />
        <ShellContent Title="Trends"   Icon="trends_tab.svg"   ... />
        <ShellContent Title="Settings" Icon="settings_tab.svg" ... />
    </TabBar>
</Shell>
```

### 6.3 `ViewModels/WizardViewModel.cs` (update)

Add the following to the existing ViewModel:

```csharp
[ObservableProperty] private bool _isRerun;

public void PrepareForRerun()
{
    IsRerun   = true;
    CurrentStep = 1;
    // reset all profile fields to null/defaults
    Gender          = null;
    Age             = null;
    HeightCm        = null;
    HeightFeet      = null;
    HeightInches    = null;
    StartingWeightKg = null;
    TargetWeightKg  = null;
    TargetPeriod    = null;
    UnitSystem      = UnitSystem.Metric;
}
```

Update `Back()` command:

```csharp
[RelayCommand]
private async Task Back()
{
    if (IsRerun && CurrentStep == 1)
    {
        WeakReferenceMessenger.Default.Send(new WizardDismissedMessage(false));
        await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
        return;
    }
    if (CurrentStep > 1)
        CurrentStep--;
}
```

Update `Next()` Step 3 completion path — add message send before `PopModalAsync`:

```csharp
// existing: after saving profile and before popping modal
WeakReferenceMessenger.Default.Send(new WizardDismissedMessage(true));
await Application.Current!.Windows[0].Page!.Navigation.PopModalAsync();
```

**Back button visibility in XAML:** Bind the Back button's `IsVisible` to `{Binding CanGoBack}` where:

```csharp
public bool CanGoBack => CurrentStep > 1 || IsRerun;
```

(Step 1 Back is shown only in re-run mode, hidden in mandatory first-launch mode.)

### 6.4 `Views/Settings/SettingsPage.xaml.cs` (update)
Wire in the ViewModel:

```csharp
public SettingsPage(SettingsViewModel viewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
}
```

Expose the ViewModel for external callers (used by `SettingsViewModel` indirectly via WizardPage — see below):

```csharp
// No extra exposure needed for SettingsPage itself.
```

### 6.5 `Views/Wizard/WizardPage.xaml.cs` (update)
Expose the ViewModel so `SettingsViewModel` can call `PrepareForRerun()` after resolving the page from DI:

```csharp
public WizardViewModel ViewModel => (WizardViewModel)BindingContext;
```

### 6.6 `ViewModels/SettingsViewModel.cs` (new)

```csharp
public partial class SettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly IServiceProvider _services;

    public SettingsViewModel(IMediator mediator, IServiceProvider services)
    {
        _mediator = mediator;
        _services = services;
    }

    [RelayCommand]
    private async Task ReRunSetupAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Re-Run the Setup?",
            "Your stats and goals will be permanently deleted.",
            "Confirm", "Cancel");

        if (!confirmed) return;

        // Snapshot existing profile before delete
        var snapshot = await _mediator.Send(new GetUserProfileQuery());

        // Delete profile — wizard will find nothing and start blank
        await _mediator.Send(new DeleteUserProfileCommand());

        // Resolve wizard page and prepare it for re-run
        var wizardPage = _services.GetRequiredService<WizardPage>();
        wizardPage.ViewModel.PrepareForRerun();

        // Arm a TaskCompletionSource to know when the wizard is done
        var tcs = new TaskCompletionSource<bool>();
        WeakReferenceMessenger.Default.Register<WizardDismissedMessage>(this, (_, m) =>
        {
            WeakReferenceMessenger.Default.Unregister<WizardDismissedMessage>(this);
            tcs.TrySetResult(m.Completed);
        });

        await Shell.Current.Navigation.PushModalAsync(wizardPage);
        bool completed = await tcs.Task;

        // If the user cancelled mid-wizard, restore the original profile
        if (!completed && snapshot is not null)
        {
            await _mediator.Send(new SaveUserProfileCommand
            {
                UnitSystem        = snapshot.UnitSystem,
                Gender            = snapshot.Gender,
                Age               = snapshot.Age,
                HeightCm          = snapshot.HeightCm,
                StartingWeightKg  = snapshot.StartingWeightKg,
                TargetWeightKg    = snapshot.TargetWeightKg,
                TargetPeriod      = snapshot.TargetPeriod,
            });
        }
    }
}
```

### 6.7 `Views/Settings/SettingsPage.xaml` (update)
Replace the placeholder label with the real Settings UI:

```xml
<ContentPage ... Title="Settings" BackgroundColor="{StaticResource ColorBase}">
    <VerticalStackLayout Padding="24,32" Spacing="0">

        <!-- Section header -->
        <Label Text="PROFILE"
               TextColor="{StaticResource ColorNickel}"
               FontSize="11" LetterSpacing="2"
               Margin="0,0,0,12" />

        <!-- Re-Run action row -->
        <Border BackgroundColor="#2A2A2A" StrokeShape="RoundRectangle 10" Stroke="Transparent">
            <Grid Padding="16,14" ColumnDefinitions="*,Auto">
                <VerticalStackLayout Grid.Column="0" Spacing="3">
                    <Label Text="Re-Run the Setup"
                           TextColor="{StaticResource ColorText}"
                           FontSize="16" FontAttributes="Bold" />
                    <Label Text="Your stats and your goals will be deleted!"
                           TextColor="{StaticResource ColorNickel}"
                           FontSize="13" />
                </VerticalStackLayout>
                <Label Grid.Column="1" Text="›"
                       TextColor="{StaticResource ColorNickel}"
                       FontSize="22" VerticalOptions="Center" />
                <Grid.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding ReRunSetupCommand}" />
                </Grid.GestureRecognizers>
            </Grid>
        </Border>

    </VerticalStackLayout>
</ContentPage>
```

### 6.8 `MauiProgram.cs` (update)
Register `SettingsViewModel` as transient (same pattern as `WizardViewModel`):

```csharp
builder.Services.AddTransient<SettingsViewModel>();
builder.Services.AddTransient<SettingsPage>();
```

---

## 7. File Additions Summary

```
src/LeanAI.Maui/Resources/Images/
  log_tab.svg                              ← new (monochrome pencil SVG)
  trends_tab.svg                           ← new (monochrome chart SVG)
  settings_tab.svg                         ← new (monochrome gear SVG)

src/LeanAI.Maui/Messages/
  WizardDismissedMessage.cs               ← new

src/LeanAI.Application/WeightManagement/Commands/DeleteUserProfile/
  DeleteUserProfileCommand.cs             ← new
  DeleteUserProfileCommandHandler.cs      ← new

src/LeanAI.Maui/ViewModels/
  SettingsViewModel.cs                    ← new

tests/LeanAI.Tests/Application/WeightManagement/
  DeleteUserProfileCommandHandlerTests.cs ← new

Modified:
  src/LeanAI.Domain/WeightManagement/Interfaces/IUserProfileRepository.cs
    └─ +DeleteAsync
  src/LeanAI.Infrastructure/Repositories/UserProfileRepository.cs
    └─ +DeleteAsync implementation
  src/LeanAI.Maui/AppShell.xaml
    └─ TabBarSelectedColor / TabBarUnselectedColor; icon refs → .svg
  src/LeanAI.Maui/Views/Settings/SettingsPage.xaml
    └─ Replace placeholder with action-row UI
  src/LeanAI.Maui/Views/Settings/SettingsPage.xaml.cs
    └─ Constructor wires SettingsViewModel
  src/LeanAI.Maui/Views/Wizard/WizardPage.xaml.cs
    └─ +ViewModel property
  src/LeanAI.Maui/ViewModels/WizardViewModel.cs
    └─ +IsRerun, +PrepareForRerun(), update Back(), update Next(), +CanGoBack
  src/LeanAI.Maui/MauiProgram.cs
    └─ Register SettingsViewModel + SettingsPage as transient
```

---

## 8. Delivery Notes

| Item | Outcome |
|---|---|
| `TabBarSelectedColor` / `TabBarUnselectedColor` in XAML | These property names do not exist in MAUI 10.0.60. Neither XAML attribute form (`Shell.TabBarSelectedColor`) nor instance property form compiled. Actual BindableProperties are `TabBarForegroundColorProperty` (active) and `TabBarUnselectedColorProperty` (inactive), accessed via `SetValue()` in `AppShell.xaml.cs` code-behind. |
| `DisplayAlert` obsolete | `Shell.Current.DisplayAlert()` is marked `[Obsolete]` in MAUI 10. Changed to `Shell.Current.DisplayAlertAsync()`. |
| `Back()` kept as `void` | The plan showed `Back()` as `async Task` for the re-run cancel path. The implementation keeps it `void` — the message send is synchronous, and the modal pop is handled by `WizardPage` on receiving `WizardDismissedMessage(false)`. No async work is needed in the ViewModel. |
| `OnAppearing` guard for re-run | `WizardPage.OnAppearing()` was updated to skip `LoadExistingAsync()` when `IsRerun == true`. Without this guard, the wizard would attempt to load the (now-deleted) profile on appear, which returns `null` and is harmless — but the guard makes intent explicit and avoids the redundant DB call. |

## 9. Definition of Done Checklist

- [x] Three tab icons render — gear (Settings), pencil (Log), chart (Trends)
- [x] Settings tab icon is Copper (`#D28B5C`) when active, Nickel (`#9A9EAB`) when inactive
- [x] Log and Trends tabs follow the same color rules
- [x] Tapping "Re-Run the Setup" shows a native confirmation dialog
- [x] Dismissing the dialog (Cancel) leaves the profile completely untouched
- [x] Confirming the dialog deletes the profile and launches the Setup Wizard from Step 1 with blank fields
- [x] Cancelling mid-wizard (Back on Step 1) restores the original profile and returns to Settings
- [x] Completing the wizard saves the new profile and dismisses to Settings tab
- [x] `DeleteUserProfileCommandHandlerTests` pass with 100% branch coverage
- [x] All pre-existing tests still pass
- [x] `dotnet build` → 0 errors, 0 warnings
- [x] `dotnet test` → all tests green
