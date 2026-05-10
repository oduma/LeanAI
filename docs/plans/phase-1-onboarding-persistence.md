# Phase 1: Onboarding & Persistence — Implementation Plan

> **Status: COMPLETE** — Delivered 2026-05-10

**Goal:** Establish the user profile, persistent storage, and the Setup Wizard UI.  
**DoD:** App launches, Wizard saves a complete profile to SQLite, solution is Runnable on Android.

---

## Decisions & Constraints

| Decision | Choice |
|---|---|
| Gender enum | `Male`, `Female` |
| Imperial height input | Two fields: Feet + Inches (ViewModel combines → stores as cm) |
| Wizard completion (Phase 1) | Save goal directly to DB after Step 3; AI validation inserted in Phase 2 |
| Autosave in wizard | Partial profile saved on each debounced field change; `IsComplete` gates wizard exit |
| Unit storage | All values stored Metric in DB (kg, cm); UI converts for display |
| Profile cardinality | Singleton — one `UserProfile` record per app |

---

## Implementation Order (TDD: Red → Green → Refactor)

```
1. Domain layer  →  2. Application layer  →  3. Infrastructure  →  4. Presentation
```

Each layer: write failing tests first, then implement to pass.

---

## 1. Asset Preparation

**Source:** `z-com-ai/`  
**Actions:**
- Copy `LeanAI-icon.png` → replace default MAUI app icon in `Resources/AppIcon/`
- Copy all four SVGs (`metric-on.svg`, `metric-off.svg`, `imperial-on.svg`, `imperial-off.svg`) → `Resources/Images/`
- **Clean `imperial-on.svg` and `imperial-off.svg`:** remove stray `<text>` editor artifacts before adding to project

---

## 2. Domain Layer (`LeanAI.Domain`)

### Folder: `WeightManagement/`

#### Enums
```
Enums/Gender.cs          → Male, Female
Enums/UnitSystem.cs      → Metric, Imperial
Enums/TargetPeriod.cs    → ThreeMonths, SixMonths, OneYear
```

`TargetPeriod` exposes a `TotalDays` property (90 / 180 / 365) so Phase 3 can compute the daily delta without switching on the enum.

#### Entity: `Entities/UserProfile.cs`
Extends `BaseEntity`. All optional fields are nullable — enables partial wizard resume across app restarts.

| Property | Type | Notes |
|---|---|---|
| `UnitSystem` | `UnitSystem` | Default `Metric` |
| `Gender` | `Gender?` | Null until Step 2 complete |
| `Age` | `int?` | |
| `HeightCm` | `double?` | Always stored in cm |
| `StartingWeightKg` | `double?` | Always stored in kg |
| `TargetWeightKg` | `double?` | Always stored in kg |
| `TargetPeriod` | `TargetPeriod?` | |
| `IsComplete` | `bool` (computed) | True only when all nullable fields have values |

#### Domain Service: `Services/UnitConverter.cs`
Static pure class. Per Code-Quality DRY rule: "Abstract common math into a Domain Service."

| Method | Signature |
|---|---|
| `KgToLb` | `static double KgToLb(double kg)` |
| `LbToKg` | `static double LbToKg(double lb)` |
| `CmToInches` | `static double CmToInches(double cm)` |
| `InchesToCm` | `static double InchesToCm(double inches)` |
| `FeetInchesToCm` | `static double FeetInchesToCm(int feet, double inches)` |
| `CmToFeetAndInches` | `static (int Feet, double Inches) CmToFeetAndInches(double cm)` |

#### Repository Interface: `Interfaces/IUserProfileRepository.cs`
```csharp
Task<UserProfile?> GetAsync(CancellationToken ct = default);
Task SaveAsync(UserProfile profile, CancellationToken ct = default);
```

### Domain Tests (`LeanAI.Tests/Domain/WeightManagement/`)

**`UserProfileTests.cs`** — 100% branch coverage on `IsComplete`:
- `IsComplete_WhenAllFieldsNull_ReturnsFalse`
- `IsComplete_WhenSomeFieldsMissing_ReturnsFalse` (one test per missing field)
- `IsComplete_WhenAllFieldsSet_ReturnsTrue`

**`UnitConverterTests.cs`** — 100% branch coverage:
- Round-trip Kg ↔ Lb (with tolerance)
- Round-trip Cm ↔ Inches (with tolerance)
- `FeetInchesToCm` → `CmToFeetAndInches` round-trip
- Edge case: zero values

---

## 3. Application Layer (`LeanAI.Application`)

### Folder: `WeightManagement/`

#### DTO: `DTOs/UserProfileDto.cs`
`record` type. Mirrors `UserProfile` properties. Used as the return type for queries.

#### Commands: `Commands/SaveUserProfile/`
- `SaveUserProfileCommand.cs` — `IRequest<Unit>` carrying all profile fields (nullable)
- `SaveUserProfileCommandHandler.cs` — gets or creates `UserProfile`, maps fields, calls `SaveAsync`

#### Queries: `Queries/GetUserProfile/`
- `GetUserProfileQuery.cs` — `IRequest<UserProfileDto?>` (no parameters)
- `GetUserProfileQueryHandler.cs` — calls `GetAsync`, maps to DTO (returns null if no profile)

#### AutoMapper: `Mappings/UserProfileMappingProfile.cs`
- `UserProfile` → `UserProfileDto` (and reverse for command mapping)

### Application Tests (`LeanAI.Tests/Application/WeightManagement/`)

All handlers tested with a **mocked** `IUserProfileRepository` (Moq).

**`SaveUserProfileCommandHandlerTests.cs`**:
- `Handle_WhenNoExistingProfile_CreatesNewAndSaves`
- `Handle_WhenProfileExists_UpdatesExistingAndSaves`

**`GetUserProfileQueryHandlerTests.cs`**:
- `Handle_WhenNoProfile_ReturnsNull`
- `Handle_WhenProfileExists_ReturnsMappedDto`

---

## 4. Infrastructure Layer (`LeanAI.Infrastructure`)

### `Persistence/LeanAIDbContext.cs`
Add `DbSet<UserProfile> UserProfiles`.  
Add `OnModelCreating` configuration for `UserProfile` table (nullable columns, enum-to-int storage).

### Migration
```
dotnet ef migrations add Initial_UserProfile --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Maui
```
Produces a migration that creates the `UserProfiles` table. Verified to apply cleanly with `dotnet ef database update`.

### `Repositories/UserProfileRepository.cs`
Implements `IUserProfileRepository`:
- `GetAsync` → `FirstOrDefaultAsync()` (singleton pattern)
- `SaveAsync` → `Add` if new, `Update` if tracked; calls `SaveChangesAsync()`

### `DependencyInjection.cs`
Extension method `AddInfrastructure(this IServiceCollection services, string dbPath)` to register:
- `LeanAIDbContext` with SQLite connection string
- `IUserProfileRepository → UserProfileRepository` (scoped)

`MauiProgram.cs` updated to call `builder.Services.AddInfrastructure(dbPath)` instead of the inline DbContext registration.

---

## 5. Presentation Layer (`LeanAI.Maui`)

### Resources

**`Resources/Colors/AppColors.xaml`** — global color dictionary:

| Key | Hex | Role |
|---|---|---|
| `ColorBase` | `#222222` | Background |
| `ColorText` | `#F0F2F5` | Text / Icons |
| `ColorCopper` | `#D28B5C` | Active / On-track |
| `ColorNickel` | `#9A9EAB` | Inactive / Off-track |

Merged into `App.xaml` resource dictionaries.

### Shell & Navigation (`AppShell.xaml`)

Three tabs with bottom tab bar:
1. **Log** → `LogPage` (placeholder in Phase 1)
2. **Trends** → `TrendsPage` (placeholder in Phase 1)
3. **Settings** → `SettingsPage` (placeholder in Phase 1)

### Startup Logic (`App.xaml.cs`)

On `CreateWindow`, before returning the Shell window:
1. Send `GetUserProfileQuery` via MediatR
2. If result is null or `IsComplete == false` → push `WizardPage` modally (blocks shell)
3. If `IsComplete == true` → open Shell directly

### Wizard (`Views/Wizard/WizardPage.xaml` + `WizardViewModel.cs`)

**Three-step stepper** — single page, content swaps by `CurrentStep` (1, 2, 3).  
A step indicator (3 dots / progress bar) is visible throughout.  
Back/Next buttons navigate steps. Next on Step 3 is disabled until `IsComplete`.

#### Step 1 — Unit Preference
- Two large tappable cards using the provided SVGs:
  - `metric-on.svg` / `metric-off.svg` for Metric
  - `imperial-on.svg` / `imperial-off.svg` for Imperial
- Only one can be active at a time (toggle behaviour)
- Selection updates `UnitSystem` on the ViewModel → triggers debounced save

#### Step 2 — User Stats
| Field | Control | Notes |
|---|---|---|
| Gender | Segmented control (2 options) | Male / Female |
| Age | `Entry` (numeric) | Integers only |
| Height | Two `Entry` fields (Feet + Inches) in Imperial; one `Entry` (cm) in Metric | Labels update dynamically based on Step 1 choice |
| Starting Weight | `Entry` (decimal) | Label shows "kg" or "lb" based on Step 1 |

All numeric entries: 500ms debounce → `SaveUserProfileCommand` dispatched via MediatR.

#### Step 3 — Goal
| Field | Control | Notes |
|---|---|---|
| Target Weight | `Entry` (decimal) | Same unit label as Starting Weight |
| Target Period | Segmented control (3 options) | 3 Months / 6 Months / 1 Year |

On Next (Step 3 → completion):
1. Final `SaveUserProfileCommand` dispatched
2. Wizard page is dismissed → Shell becomes active

### ViewModel: `WizardViewModel.cs`
Uses `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).

Key members:
- `[ObservableProperty] int currentStep` (1–3)
- `[ObservableProperty] UnitSystem unitSystem`
- `[ObservableProperty] Gender? gender`
- `[ObservableProperty] int? age`
- `[ObservableProperty] double? heightCm` (always metric internally)
- `[ObservableProperty] int? heightFeet` + `[ObservableProperty] double? heightInches` (Imperial display)
- `[ObservableProperty] double? startingWeightKg`
- `[ObservableProperty] double? targetWeightKg`
- `[ObservableProperty] TargetPeriod? targetPeriod`
- `[RelayCommand] Task Next()` — advances step or completes wizard
- `[RelayCommand] void Back()` — goes to previous step
- `bool CanGoNext` — computed, gates the Next button

Debounce: a `CancellationTokenSource` per numeric field, reset on each change, fires save after 500ms.

---

## 6. File Additions Summary

```
src/LeanAI.Domain/WeightManagement/
  Enums/Gender.cs
  Enums/UnitSystem.cs
  Enums/TargetPeriod.cs
  Entities/UserProfile.cs
  Interfaces/IUserProfileRepository.cs
  Services/UnitConverter.cs

src/LeanAI.Application/WeightManagement/
  DTOs/UserProfileDto.cs
  Commands/SaveUserProfile/SaveUserProfileCommand.cs
  Commands/SaveUserProfile/SaveUserProfileCommandHandler.cs
  Queries/GetUserProfile/GetUserProfileQuery.cs
  Queries/GetUserProfile/GetUserProfileQueryHandler.cs
  Mappings/UserProfileMappingProfile.cs

src/LeanAI.Infrastructure/
  Persistence/LeanAIDbContext.cs         ← updated
  Repositories/UserProfileRepository.cs
  DependencyInjection.cs

src/LeanAI.Maui/
  Resources/Colors/AppColors.xaml
  Resources/Images/metric-on.svg         ← from z-com-ai
  Resources/Images/metric-off.svg
  Resources/Images/imperial-on.svg       ← cleaned (stray text removed)
  Resources/Images/imperial-off.svg      ← cleaned (stray text removed)
  Resources/AppIcon/                     ← replaced with LeanAI-icon.png
  Views/Wizard/WizardPage.xaml + .cs
  Views/Log/LogPage.xaml + .cs           ← placeholder
  Views/Trends/TrendsPage.xaml + .cs     ← placeholder
  Views/Settings/SettingsPage.xaml + .cs ← placeholder
  ViewModels/WizardViewModel.cs
  AppShell.xaml                          ← updated (3 tabs)
  App.xaml                               ← updated (AppColors merged)
  App.xaml.cs                            ← updated (startup logic)
  MauiProgram.cs                         ← updated (AddInfrastructure)

tests/LeanAI.Tests/
  Domain/WeightManagement/UserProfileTests.cs
  Domain/WeightManagement/UnitConverterTests.cs
  Application/WeightManagement/SaveUserProfileCommandHandlerTests.cs
  Application/WeightManagement/GetUserProfileQueryHandlerTests.cs
```

---

## 7. Delivery Notes

| Item | Outcome |
|---|---|
| `Resources/Colors/AppColors.xaml` | Consolidated into `Resources/Styles/Colors.xaml` — separate subfolder not embedded by Android build |
| `App(IMediator, WizardPage)` constructor | Changed to `App(IMediator, IServiceProvider)` — DI resolves `WizardPage` lazily to avoid `StaticResource` crash before `App.InitializeComponent()` runs |
| Splash screen | Replaced default .NET SVG with custom `splash.png` (LeanAI icon + "LeanAI" text in copper on dark background) |
| Android manifest icon refs | Updated from `@mipmap/appicon` to `@mipmap/leanai_icon` to match Resizetizer output name |

## 8. Definition of Done Checklist

- [x] All Domain and Application tests pass with 100% branch coverage
- [x] EF Core migration `Initial_UserProfile` applies cleanly (`dotnet ef database update`)
- [x] App launches on Android emulator/device
- [x] Wizard is shown automatically on first launch (no profile)
- [x] All 3 wizard steps are navigable (Back/Next)
- [x] Unit toggle on Step 1 dynamically updates labels on Steps 2 and 3
- [x] Height entry uses two fields (Feet + Inches) in Imperial mode
- [x] Partial wizard data survives app restart (resume at correct step)
- [x] Completing Step 3 saves a complete profile and dismisses the wizard
- [x] Shell with 3 tabs is shown after wizard completion
- [x] `dotnet build` → 0 errors, 0 warnings
- [x] `dotnet test` → all tests green
