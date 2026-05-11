# Phase 3.5 Plan: AI Configuration Settings

> **Status: COMPLETE** — Delivered 2026-05-11

---

## 1. Goal

Expose two user-editable fields on the Settings screen — the Gemini model name and the Gemini API key — so users can update AI configuration without reinstalling the app. The model name is persisted to SQLite via a new `AppSettings` entity; the API key stays in `SecureStorage` only. After saving, all subsequent AI calls in the same session use the new values immediately (hot reload).

---

## 2. Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Settings storage | New `AppSettings` entity in DB | More settings will follow; an entity scales cleanly |
| API key storage | SecureStorage only — never in DB | Security requirement (TECHNICAL_STANDARDS §2) |
| Application → SecureStorage boundary | `IApiKeyStorage` Domain interface + Infrastructure impl | Keeps handlers platform-agnostic; follows Dependency Rule |
| Hot reload mechanism | `GeminiKeyHolder` extended to carry model name | Already the bridge between startup provisioning and `IChatClient`; minimal surface change |
| Single-row table pattern | Same as `UserProfile` | Consistent with existing codebase |
| Default model | `"gemini-2.5-flash"` | Matches current hardcoded value in Infrastructure |

---

## 3. Affected Files & New Artifacts

### Domain Layer (`LeanAI.Domain`)

| File | Action |
|---|---|
| `WeightManagement/Entities/AppSettings.cs` | **New** — entity with `GeminiModelName` |
| `WeightManagement/Interfaces/IAppSettingsRepository.cs` | **New** — `GetAsync` / `SaveAsync` |
| `WeightManagement/Interfaces/IApiKeyStorage.cs` | **New** — `GetAsync(keyName)` / `SetAsync(keyName, value)` |

### Infrastructure Layer (`LeanAI.Infrastructure`)

| File | Action |
|---|---|
| `Persistence/LeanAIDbContext.cs` | **Edit** — add `DbSet<AppSettings>`, `OnModelCreating` config |
| `Repositories/AppSettingsRepository.cs` | **New** — single-row EF Core repository |
| `DependencyInjection.cs` | **Edit** — `GeminiKeyHolder` made `public`, gains `ModelName`; `IChatClient` changed from `AddSingleton` → `AddTransient` (reads live key + model per resolution); `SetGeminiModelName` extension added; `IAppSettingsRepository` registered |
| `Migrations/20260511185341_Add_AppSettings.cs` | **New** — adds `AppSettings` table only; no existing tables altered |

> **Architecture note:** `SecureStorageApiKeyStorage` was moved to the MAUI project because Infrastructure targets plain `net10.0` and cannot access `SecureStorage`. The `IApiKeyStorage` interface stays in Domain; the implementation is registered in `MauiProgram.cs`.

### Application Layer (`LeanAI.Application`)

| File | Action |
|---|---|
| `WeightManagement/DTOs/AppSettingsDto.cs` | **New** — record with `GeminiModelName`, `GeminiApiKey` |
| `WeightManagement/Queries/GetAppSettings/GetAppSettingsQuery.cs` | **New** |
| `WeightManagement/Queries/GetAppSettings/GetAppSettingsQueryHandler.cs` | **New** |
| `WeightManagement/Commands/SaveAppSettings/SaveAppSettingsCommand.cs` | **New** |
| `WeightManagement/Commands/SaveAppSettings/SaveAppSettingsCommandHandler.cs` | **New** |
| `Common/ApiKeyNames.cs` | **New** — centralises `"gemini_key"` storage key constant |

### Presentation Layer (`LeanAI.Maui`)

| File | Action |
|---|---|
| `Services/SecureStorageApiKeyStorage.cs` | **New** — wraps MAUI `SecureStorage`; on `SetAsync` for the Gemini key also updates `GeminiKeyHolder` for hot reload |
| `ViewModels/SettingsViewModel.cs` | **Edit** — `LoadAiSettingsCommand`, `SaveAiSettingsCommand` (with `CanExecute = HasChanges`), `HasChanges` tracking via `OnGeminiModelNameChanged` / `OnGeminiApiKeyChanged` partial methods; after save resets `HasChanges = false` |
| `Views/Settings/SettingsPage.xaml` | **Edit** — "AI Configuration" section; button labelled "Save Settings"; background bound to `HasChanges` via `BoolToColorConverter` (Copper when changed, Nickel when idle) |
| `Views/Settings/SettingsPage.xaml.cs` | **Edit** — `OnAppearing` fires `LoadAiSettingsCommand` |
| `MauiProgram.cs` | **Edit** — registers `IApiKeyStorage → SecureStorageApiKeyStorage` as singleton |
| `App.xaml.cs` | **Edit** — `ProvisionAiSettingsAsync` also loads `GeminiModelName` from `IAppSettingsRepository` and pushes it into `GeminiKeyHolder` |

### Tests (`LeanAI.Tests`)

| File | Action |
|---|---|
| `Application/GetAppSettingsQueryHandlerTests.cs` | **New** — 100% branch coverage |
| `Application/SaveAppSettingsCommandHandlerTests.cs` | **New** — 100% branch coverage |

---

## 4. Implementation Steps (TDD Order)

### Step 1 — Domain: `AppSettings` Entity

**File:** `src/LeanAI.Domain/WeightManagement/Entities/AppSettings.cs`

```csharp
public sealed class AppSettings : BaseEntity
{
    public const string DefaultModelName = "gemini-2.5-flash";

    public string GeminiModelName { get; set; } = DefaultModelName;
}
```

- Extends `BaseEntity` (inherits `Guid Id`).
- `DefaultModelName` constant centralises the default value for use in App startup and handler defaults.
- No API key field — SecureStorage is authoritative.

---

### Step 2 — Domain: `IAppSettingsRepository`

**File:** `src/LeanAI.Domain/WeightManagement/Interfaces/IAppSettingsRepository.cs`

```csharp
public interface IAppSettingsRepository
{
    Task<AppSettings?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
```

Single-row pattern: `GetAsync` returns `null` if no row exists yet (first run after migration).

---

### Step 3 — Domain: `IApiKeyStorage`

**File:** `src/LeanAI.Domain/WeightManagement/Interfaces/IApiKeyStorage.cs`

```csharp
public interface IApiKeyStorage
{
    Task<string?> GetAsync(string keyName, CancellationToken ct = default);
    Task SetAsync(string keyName, string value, CancellationToken ct = default);
}
```

Generic key-name design allows future keys (e.g., a secondary service key) without a new interface.

---

### Step 4 — Application: DTOs & CQRS Artifacts

**`AppSettingsDto`** (`src/LeanAI.Application/WeightManagement/DTOs/AppSettingsDto.cs`):

```csharp
public sealed record AppSettingsDto(string GeminiModelName, string GeminiApiKey);
```

---

**`GetAppSettingsQuery`** (`…/Queries/GetAppSettings/GetAppSettingsQuery.cs`):

```csharp
public sealed record GetAppSettingsQuery : IRequest<AppSettingsDto>;
```

**`GetAppSettingsQueryHandler`**:

```csharp
public sealed class GetAppSettingsQueryHandler(
    IAppSettingsRepository settingsRepository,
    IApiKeyStorage apiKeyStorage)
    : IRequestHandler<GetAppSettingsQuery, AppSettingsDto>
{
    public async Task<AppSettingsDto> Handle(GetAppSettingsQuery request, CancellationToken ct)
    {
        var settings = await settingsRepository.GetAsync(ct);
        var modelName = settings?.GeminiModelName ?? AppSettings.DefaultModelName;
        var apiKey = await apiKeyStorage.GetAsync(ApiKeyNames.Gemini, ct) ?? string.Empty;
        return new AppSettingsDto(modelName, apiKey);
    }
}
```

> **Branch logic to test:**
> 1. `settings` is null → uses `DefaultModelName`
> 2. `settings` has a value → uses stored model name
> 3. `apiKey` is null → returns `string.Empty`
> 4. `apiKey` has a value → returns stored key

---

**`SaveAppSettingsCommand`** (`…/Commands/SaveAppSettings/SaveAppSettingsCommand.cs`):

```csharp
public sealed record SaveAppSettingsCommand(
    string GeminiModelName,
    string GeminiApiKey) : IRequest<Unit>;
```

**`SaveAppSettingsCommandHandler`**:

```csharp
public sealed class SaveAppSettingsCommandHandler(
    IAppSettingsRepository settingsRepository,
    IApiKeyStorage apiKeyStorage)
    : IRequestHandler<SaveAppSettingsCommand, Unit>
{
    public async Task<Unit> Handle(SaveAppSettingsCommand request, CancellationToken ct)
    {
        var settings = await settingsRepository.GetAsync(ct) ?? new AppSettings();
        settings.GeminiModelName = request.GeminiModelName;
        await settingsRepository.SaveAsync(settings, ct);
        await apiKeyStorage.SetAsync(ApiKeyNames.Gemini, request.GeminiApiKey, ct);
        return Unit.Value;
    }
}
```

> **Branch logic to test:**
> 1. No existing `AppSettings` row → creates a new entity, saves
> 2. Existing `AppSettings` row → updates model name, saves
> 3. `apiKeyStorage.SetAsync` is always called

---

**`ApiKeyNames` constant class** (place in `Application/Common/` or `Domain/Common/`):

```csharp
public static class ApiKeyNames
{
    public const string Gemini = "gemini_key";
}
```

Centralises the storage key string (currently duplicated between `App.xaml.cs` and `GeminiGoalValidationService`).

---

### Step 5 — Tests (TDD: write before implementation)

**`GetAppSettingsQueryHandlerTests`:**
- Mock `IAppSettingsRepository` and `IApiKeyStorage`.
- 4 test cases (branches listed above).
- Use FluentAssertions; AAA pattern.

**`SaveAppSettingsCommandHandlerTests`:**
- Mock `IAppSettingsRepository` and `IApiKeyStorage`.
- Test create-path: `GetAsync` returns null → `new AppSettings()` saved with correct model name, `SetAsync` called.
- Test update-path: `GetAsync` returns existing entity → entity updated, saved, `SetAsync` called.
- Verify `SetAsync` is called with `ApiKeyNames.Gemini` and the provided key in both paths.

---

### Step 6 — Infrastructure: `GeminiKeyHolder` (extend)

**File:** `src/LeanAI.Infrastructure/Services/GeminiKeyHolder.cs`

Add `string? GeminiModelName` property alongside the existing key property. The lazy `IChatClient` factory (in `DependencyInjection.cs`) reads `holder.GeminiModelName ?? AppSettings.DefaultModelName` when creating the client.

This means a model name change followed by a new AI call will create a fresh `IChatClient` with the new model — which is the hot-reload mechanism.

> **Note:** If `IChatClient` is currently a singleton, it must be changed to a factory/transient registration so it can pick up the new model name at next resolution. Verify the current registration scope in `DependencyInjection.cs`.

---

### Step 7 — Infrastructure: `AppSettingsRepository`

**File:** `src/LeanAI.Infrastructure/Repositories/AppSettingsRepository.cs`

Single-row EF Core implementation, structurally identical to `UserProfileRepository`:

```csharp
public sealed class AppSettingsRepository(LeanAIDbContext context) : IAppSettingsRepository
{
    public Task<AppSettings?> GetAsync(CancellationToken ct = default) =>
        context.AppSettings.FirstOrDefaultAsync(ct);

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var tracked = context.ChangeTracker.Entries<AppSettings>()
            .Any(e => e.Entity.Id == settings.Id);

        if (!tracked)
        {
            var exists = await context.AppSettings
                .AsNoTracking()
                .AnyAsync(s => s.Id == settings.Id, ct);

            if (exists) context.AppSettings.Update(settings);
            else context.AppSettings.Add(settings);
        }

        await context.SaveChangesAsync(ct);
    }
}
```

---

### Step 8 — Infrastructure: `SecureStorageApiKeyStorage`

**File:** `src/LeanAI.Infrastructure/Services/SecureStorageApiKeyStorage.cs`

```csharp
public sealed class SecureStorageApiKeyStorage(GeminiKeyHolder keyHolder) : IApiKeyStorage
{
    public async Task<string?> GetAsync(string keyName, CancellationToken ct = default) =>
        await SecureStorage.GetAsync(keyName);

    public async Task SetAsync(string keyName, string value, CancellationToken ct = default)
    {
        await SecureStorage.SetAsync(keyName, value);
        if (keyName == ApiKeyNames.Gemini)
            keyHolder.SetApiKey(value);
    }
}
```

> `SecureStorage` is a MAUI API and does not accept a `CancellationToken`. The parameter is kept for interface consistency.

---

### Step 9 — Infrastructure: EF Core — `LeanAIDbContext` + Migration

**Edit `LeanAIDbContext`:**

```csharp
public DbSet<AppSettings> AppSettings { get; } = Set<AppSettings>();
```

Add configuration in `OnModelCreating`:

```csharp
modelBuilder.Entity<AppSettings>(b =>
{
    b.HasKey(s => s.Id);
    b.Ignore(s => s.DomainEvents);
    b.Property(s => s.GeminiModelName).IsRequired();
});
```

**Generate migration:**

```bash
dotnet ef migrations add Add_AppSettings \
  --project src/LeanAI.Infrastructure \
  --startup-project src/LeanAI.Maui
```

Verify the generated migration:
- Creates `AppSettings` table with `Id` (TEXT PK) and `GeminiModelName` (TEXT NOT NULL).
- Does **not** touch `UserProfiles`, `DailyIdealWeights`, or `DailyActualWeights`.

---

### Step 10 — Infrastructure: `DependencyInjection.cs` (register new services)

```csharp
services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();
services.AddScoped<IApiKeyStorage, SecureStorageApiKeyStorage>();
```

---

### Step 11 — App Startup: `App.xaml.cs` (extend provisioning)

Extend the existing `ProvisionGeminiKeyAsync` (or add a peer method) to also load the model name:

```csharp
private async Task ProvisionAiSettingsAsync()
{
    // Existing key provisioning (unchanged)
    var key = await SecureStorage.GetAsync(ApiKeyNames.Gemini);
    if (string.IsNullOrEmpty(key))
    {
        key = GeminiBootstrapKey;
        await SecureStorage.SetAsync(ApiKeyNames.Gemini, key);
    }
    _services.SetGeminiApiKey(key);

    // New: load model name from DB
    using var scope = _services.CreateScope();
    var settingsRepo = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
    var settings = await settingsRepo.GetAsync();
    _services.SetGeminiModelName(settings?.GeminiModelName ?? AppSettings.DefaultModelName);
}
```

Add corresponding `SetGeminiModelName(string)` method to whatever extension/helper currently exposes `SetGeminiApiKey`.

---

### Step 12 — Presentation: `SettingsViewModel` (extend)

```csharp
[ObservableProperty] private string _geminiModelName = string.Empty;
[ObservableProperty] private string _geminiApiKey = string.Empty;

// Called from OnAppearing override or INavigatedToAware
private async Task LoadAiSettingsAsync()
{
    var dto = await _mediator.Send(new GetAppSettingsQuery());
    GeminiModelName = dto.GeminiModelName;
    GeminiApiKey = dto.GeminiApiKey;
}

[RelayCommand]
private async Task SaveAiSettingsAsync()
{
    await _mediator.Send(new SaveAppSettingsCommand(GeminiModelName, GeminiApiKey));
}
```

---

### Step 13 — Presentation: `SettingsPage.xaml` (extend)

Add a new section below the existing "Re-Run the Setup" row:

```
┌─────────────────────────────────────────┐
│  Re-Run the Setup          (existing)   │
│  Your stats and goals will be deleted!  │
├─────────────────────────────────────────┤
│  AI Configuration          (new section)│
│                                         │
│  AI Model                               │
│  [ gemini-2.5-flash              ]      │
│                                         │
│  Gemini API Key                         │
│  [ ••••••••••••••••••••••••••••• ]      │
│                                         │
│  [ Save AI Settings ]                   │
└─────────────────────────────────────────┘
```

- Section header styled consistently with existing Settings headers.
- API Key `Entry` uses `IsPassword="True"`.
- "Save AI Settings" button bound to `SaveAiSettingsCommand`.

---

## 5. Implementation Order (sequenced for TDD)

```
1. Domain entities & interfaces       (AppSettings, IAppSettingsRepository, IApiKeyStorage)
2. Application DTOs                   (AppSettingsDto, ApiKeyNames)
3. Tests — GetAppSettingsQueryHandler (Red)
4. GetAppSettingsQuery + Handler      (Green)
5. Tests — SaveAppSettingsCommandHandler (Red)
6. SaveAppSettingsCommand + Handler   (Green)
7. dotnet test → all green
8. Infrastructure: GeminiKeyHolder extension
9. Infrastructure: AppSettingsRepository
10. Infrastructure: SecureStorageApiKeyStorage
11. Infrastructure: LeanAIDbContext + EF migration
12. Infrastructure: DependencyInjection.cs
13. App.xaml.cs: startup provisioning extension
14. SettingsViewModel: load + save
15. SettingsPage.xaml: AI Configuration section
16. dotnet build → 0 errors, 0 warnings
17. dotnet test → all green
```

---

## 6. Definition of Done

- [x] Settings screen shows current AI model name and masked API key on open.
- [x] "Save Settings" persists model name to `AppSettings` table and key to SecureStorage.
- [x] Subsequent AI calls after save use the new model/key without restarting the app.
- [x] EF Core migration applies cleanly; no data loss to existing tables.
- [x] `GetAppSettingsQueryHandler` tested at 100% branch coverage (4 tests).
- [x] `SaveAppSettingsCommandHandler` tested at 100% branch coverage (2 tests).
- [x] `dotnet build` → 0 errors, 0 warnings.
- [x] `dotnet test` → all 50 tests green (6 new tests added).

## 7. Post-Delivery Refinements

The following changes were applied after the initial delivery, in the same session:

| Change | Detail |
|---|---|
| Button label | Renamed "Save AI Settings" → "Save Settings" |
| Disabled state | Button is disabled and appears Nickel (`#9A9EAB`) when no fields have changed; turns Copper (`#D28B5C`) and becomes tappable as soon as either field is edited |
| Implementation | `HasChanges` bool observable property; `[NotifyCanExecuteChangedFor(nameof(SaveAiSettingsCommand))]`; `partial void OnGeminiModelNameChanged` / `OnGeminiApiKeyChanged` update it; after a successful save `_loadedModelName` / `_loadedApiKey` are updated and `HasChanges` resets to `false`; XAML binds `BackgroundColor` via existing `BoolToColorConverter` with parameter `ColorCopper\|ColorNickel` |

---

## 8. Out of Scope

- Input validation (empty model name, invalid key format) — not in requirements; defer to future phase.
- "Test Connection" / validation of key against Gemini API — future enhancement.
- Encrypting the model name in the DB — not sensitive; SecureStorage is reserved for the key only.
