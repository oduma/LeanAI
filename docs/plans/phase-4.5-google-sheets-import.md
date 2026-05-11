# Phase 4.5: External Data Integration — Google Sheets Import ✅ COMPLETE

> **Status:** Shipped 2026-05-12. All 79 tests pass. See [release notes](../release-notes/v4.5-google-sheets-import.md) for a full account of changes from original scope.

---

## Scope Changes from Original Plan

The following were clarified or discovered during implementation and differ from what was planned above.

| # | Area | Original plan | As implemented |
|---|------|--------------|----------------|
| SC-1 | `lastDate` definition | Last date **with a recorded weight** | Last date **in the spreadsheet**, including future rows with no weight yet |
| SC-2 | Import classification | Two buckets: Imported / Skipped / Failed | Two-phase: all dated rows → ideal path; weighted rows only → DailyActualWeight |
| SC-3 | `ImportSummaryDto` shape | `(Imported, Skipped, Failed, FirstDate, LastDate)` | `(TotalDatesFound, IdealDatesImported, DatesWithWeightsFound, WeightRowsImported, Failed, FirstDate, LastDate)` |
| SC-4 | Step 5 summary UI | 3 lines (imported / skipped / failed) | Two-section display: Dates section + Actual Weights section with 4 metrics |
| SC-5 | Google Sheets range | `"2:{int.MaxValue}"` | Capped at `"2:401"` (400 data rows) — Google Sheets API rejects unbounded row indices |
| SC-6 | Date parsing | 6 explicit formats | 16 explicit formats + `DateTime.TryParse` fallback for locale-specific strings |
| SC-7 | Token abstraction | `GoogleOAuthService` called `SecureStorage` directly | `IGoogleTokenStorage` interface extracted in Application layer; `SecureGoogleTokenStorage` in MAUI layer (mirrors `IApiKeyStorage` pattern) |
| SC-8 | OAuth client type | Web application assumed | Android application type required — Google blocks custom URI schemes on Web clients |
| SC-9 | Step 5 spinner | Spinner inside Step 5 container, `CurrentStep` set after import | `CurrentStep = 5` moved to **before** the import call so spinner is visible during the operation |
| SC-10 | Empty-weight rows | Classified as Skipped | Rows with a valid date but no weight count toward `TotalDatesFound` and the ideal path, but are not written to `DailyActualWeight` |

---

## Phase Summary

Enable users to migrate historical weight data from a Google Sheet into LeanAI. After import, the ideal weight line is automatically recalculated to span the exact date range found in the sheet, giving the user immediate longitudinal context from day one of their data.

---

## Requirements

### From FUNCTIONAL_REQUIREMENTS.md

| # | Requirement |
|---|-------------|
| FR-1 | "Sign in with Google" OAuth 2.0 flow with `spreadsheets.readonly` + `drive.readonly` scope |
| FR-2 | Browse and select a spreadsheet from the user's Google Drive |
| FR-3 | Semi-flexible column mapping: user maps which column is Date, Weight, and Notes |
| FR-4 | Support Metric and Imperial units in the sheet (convert to kg before insertion) |
| FR-5 | Cleanse data: skip empty rows and non-numeric weights; track failure count |
| FR-6 | Bulk-insert into `DailyActualWeights`, prevent duplicates per the overwrite rule |

### User-Specified Additions

| # | Requirement |
|---|-------------|
| UA-1 | **New start date**: `UserProfile.GoalStartDate` = first date with a recorded weight in the sheet |
| UA-2 | **New end date**: `UserProfile.GoalEndDate` = last date appearing in the sheet (absolute end, regardless of whether it has a weight) |
| UA-3 | **Ideal path reset**: after import, delete all `DailyIdealWeights` and regenerate them across the new date span using `UserProfile.TargetWeightKg` (unchanged) and the first imported entry's weight as starting weight |
| UA-4 | **Pre-import warning**: before any data is written, show an explicit confirmation dialog listing every consequence (see §UI Flow §Step 4) |
| UA-5 | **Overwrite rule**: an existing `DailyActualWeight` entry is overwritten ONLY if the imported weight for that date is non-null AND > 0. Zero or null → preserve the existing app entry |

---

## Design Decisions

### D1 — GoalStartDate / GoalEndDate in UserProfile

`UserProfile` gains two new nullable columns. When both are set (import path), they take precedence over the `TargetPeriod` enum for ideal-path generation. When null (wizard path), the existing `TargetPeriod` enum logic applies unchanged.

```
UserProfile
  + GoalStartDate  DateOnly?   -- null until first import
  + GoalEndDate    DateOnly?   -- null until first import
```

`TargetPeriod` is kept as-is — it remains informational and is not deleted.

### D2 — GenerateIdealPathCommand Extension

The existing `GenerateIdealPathCommand` uses `TargetPeriod.TotalDays()`. A new optional parameter `ExactTotalDays` is added. When provided, it overrides the enum. Backward-compatible — no changes needed at existing call sites.

```csharp
public record GenerateIdealPathCommand(
    DateOnly     StartDate,
    double       StartingWeightKg,
    double       TargetWeightKg,
    TargetPeriod TargetPeriod,
    int?         ExactTotalDays = null   // new; overrides TargetPeriod.TotalDays() when set
) : IRequest<Unit>;
```

### D3 — OAuth Strategy (MAUI Android)

| Step | Mechanism |
|------|-----------|
| Initial auth | `WebAuthenticator.AuthenticateAsync` opens Google's OAuth consent page in a Chrome Custom Tab |
| Auth code → tokens | HTTP POST to `https://oauth2.googleapis.com/token` (exchange code for access + refresh token) |
| Token storage | Refresh token in `SecureStorage` under key `google_refresh_token`. Access token in memory only. |
| Silent refresh | On each import session, if a refresh token exists, exchange it for a new access token before calling any API |
| Revocation | "Disconnect Google" row in Settings clears the stored token; user must sign in again |

### D4 — Column Mapping

After spreadsheet selection, the app reads row 1 as headers. The user is shown three dropdowns:

```
Date column:    [ — pick — ▾ ]    (required; must parse to a date)
Weight column:  [ — pick — ▾ ]    (required; must parse to a decimal)
Notes column:   [ — pick — ▾ ]    (optional; any text)
```

Each dropdown contains the detected header names (columns A, B, C … displayed as the header value or "Column A" if no header row).

### D5 — Overwrite Rule (exact logic)

For each parsed row:

```
if importedWeight is null OR importedWeight <= 0:
    skippedCount++
    continue                          // preserve existing app entry, if any

existing = repo.GetByDateAsync(row.Date)

if existing is null:
    INSERT new DailyActualWeight
    importedCount++
else:
    UPDATE existing (WeightKg = importedWeight, Notes = importedNotes)
    importedCount++
```

---

## Prerequisites (External Setup — One-Time, Outside the Code)

1. **Google Cloud Project** — enable the *Google Sheets API* and *Google Drive API*.
2. **OAuth 2.0 Client ID** — create an Android credential for package `com.leanai.app`. Download the `google-services.json`.
3. **Redirect URI** — register a Custom URL scheme (e.g. `com.leanai.app:/oauth2redirect`) in the Cloud Console and in `AndroidManifest.xml`.
4. **NuGet packages** to add:
   - `Google.Apis.Sheets.v4` (Infrastructure)
   - `Google.Apis.Drive.v3` (Infrastructure)
   - `Google.Apis.Auth` (Infrastructure)

---

## Architecture Components

### Domain Layer

**`UserProfile.cs`** — two new properties:
```csharp
public DateOnly? GoalStartDate { get; set; }
public DateOnly? GoalEndDate   { get; set; }
```

**`IDailyActualWeightRepository.cs`** — one new method:
```csharp
Task InsertBatchAsync(IEnumerable<DailyActualWeight> entries, CancellationToken ct = default);
```

**`IGoogleSheetsService.cs`** (Application layer, following the `IAIGoalValidationService` pattern):
```csharp
public interface IGoogleSheetsService
{
    Task<IReadOnlyList<SpreadsheetSummary>> GetSpreadsheetsAsync(string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetColumnHeadersAsync(string spreadsheetId, string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<SheetRow>> GetRowsAsync(string spreadsheetId, string dateCol, string weightCol, string? notesCol, string accessToken, CancellationToken ct = default);
}

public record SpreadsheetSummary(string Id, string Name);

public record SheetRow(DateOnly? Date, double? WeightKg, string? Notes, bool ParseFailed);
```

### Application Layer

| File | Purpose |
|------|---------|
| `WeightManagement/DTOs/ImportSummaryDto.cs` | `record ImportSummaryDto(int Imported, int Skipped, int Failed, DateOnly FirstDate, DateOnly LastDate)` |
| `WeightManagement/Commands/UpdateGoalFromImport/UpdateGoalFromImportCommand.cs` | Updates `GoalStartDate`, `GoalEndDate`, `StartingWeightKg` on `UserProfile` |
| `WeightManagement/Commands/UpdateGoalFromImport/UpdateGoalFromImportCommandHandler.cs` | Loads profile, sets the three fields, upserts back |
| `WeightManagement/Commands/ImportGoogleSheets/ImportGoogleSheetsCommand.cs` | Full import orchestration input |
| `WeightManagement/Commands/ImportGoogleSheets/ImportGoogleSheetsCommandHandler.cs` | Orchestrates all steps; returns `ImportSummaryDto` |
| `WeightManagement/Queries/GetGoogleSpreadsheets/GetGoogleSpreadsheetsQuery.cs` | Wraps `IGoogleSheetsService.GetSpreadsheetsAsync` |
| `WeightManagement/Queries/GetGoogleSpreadsheets/GetGoogleSpreadsheetsQueryHandler.cs` | |
| `WeightManagement/Queries/GetSheetColumns/GetSheetColumnsQuery.cs` | Returns column headers for the mapping step |
| `WeightManagement/Queries/GetSheetColumns/GetSheetColumnsQueryHandler.cs` | |
| `WeightManagement/Services/IGoogleSheetsService.cs` | Interface (defined here, implemented in Infrastructure) |
| *(modified)* `GenerateIdealPathCommand.cs` | Add `int? ExactTotalDays = null` parameter |
| *(modified)* `GenerateIdealPathCommandHandler.cs` | `var totalDays = request.ExactTotalDays ?? request.TargetPeriod.TotalDays();` |

**`ImportGoogleSheetsCommand` (parameters):**
```csharp
public record ImportGoogleSheetsCommand(
    string   SpreadsheetId,
    string   DateColumn,
    string   WeightColumn,
    string?  NotesColumn,
    UnitSystem SheetUnitSystem,   // Metric or Imperial (user selects during mapping step)
    string   AccessToken
) : IRequest<ImportSummaryDto>;
```

**`ImportGoogleSheetsCommandHandler` (orchestration):**
```
1. sheetsService.GetRowsAsync(...)
2. Parse rows → separate valid, skipped, failed
3. Get existing actual entries for the date range (GetRangeAsync)
4. Apply overwrite rule → build inserts + updates lists
5. BulkInsert new entries
6. UpsertAsync each updated entry
7. UpdateGoalFromImportCommand(firstDate, lastDate, firstWeight)
8. GenerateIdealPathCommand(startDate=firstDate, startingWeightKg=firstWeight, targetWeightKg=profile.TargetWeightKg, TargetPeriod=profile.TargetPeriod, ExactTotalDays=(lastDate-firstDate).Days+1)
9. Return ImportSummaryDto
```

### Infrastructure Layer

| File | Purpose |
|------|---------|
| `Services/GoogleSheetsService.cs` | Implements `IGoogleSheetsService` using `Google.Apis.Sheets.v4` and `Google.Apis.Drive.v3` |
| `Services/GoogleOAuthService.cs` | Token exchange, silent refresh, token storage via `SecureStorage` |
| *(modified)* `Repositories/DailyActualWeightRepository.cs` | Add `InsertBatchAsync` implementation |
| *(modified)* `DependencyInjection.cs` | Register `IGoogleSheetsService`, `GoogleOAuthService` |

**`GoogleOAuthService` public contract:**
```csharp
public class GoogleOAuthService(IApiKeyStorage _)   // follows existing SecureStorage pattern
{
    public Task<string?> GetRefreshTokenAsync();
    public Task SetRefreshTokenAsync(string token);
    public Task ClearRefreshTokenAsync();
    public Task<string> ExchangeAuthCodeAsync(string code, string codeVerifier);   // PKCE
    public Task<string> RefreshAccessTokenAsync(string refreshToken);
}
```

### MAUI (Presentation) Layer

| File | Purpose |
|------|---------|
| `Views/Import/ImportWizardPage.xaml` | Multi-step import page (modal) |
| `Views/Import/ImportWizardPage.xaml.cs` | Code-behind, pushes modal, listens for dismiss |
| `ViewModels/ImportWizardViewModel.cs` | All 5 steps as a state machine |
| *(modified)* `Views/Settings/SettingsPage.xaml` | Add "Import from Google Sheets" row + "Disconnect Google" row |
| *(modified)* `ViewModels/SettingsViewModel.cs` | Add `OpenImportWizardCommand`, `DisconnectGoogleCommand` |

---

## UI Flow (Screen by Screen)

### Entry Point — Settings Page (new rows, below "AI Configuration")

```
┌─────────────────────────────────────────────────────────────┐
│  DATA IMPORT                                                │
│  Import from Google Sheets                      [  ▶  ]    │
│  Sync your historical weight data                           │
│──────────────────────────────────────────────────────────── │
│  Disconnect Google Account                      [  ▶  ]    │ (visible only when token stored)
│  Requires re-authentication on next import                  │
└─────────────────────────────────────────────────────────────┘
```

### Import Wizard — Step 1: Sign In

- Copper "Sign in with Google" button
- If refresh token already stored: skip to Step 2 automatically (silent refresh)
- On button tap: `WebAuthenticator.AuthenticateAsync` → Chrome Custom Tab → Google consent
- On success: exchange code → store refresh token → advance to Step 2

### Import Wizard — Step 2: Select Spreadsheet

- List of spreadsheets from Google Drive (name only)
- Loading indicator (Copper spinner) while fetching
- Tap a row → store selected `SpreadsheetId` → advance to Step 3

### Import Wizard — Step 3: Map Columns

```
┌─────────────────────────────────────────────────────────────┐
│  MAP COLUMNS                                                │
│                                                             │
│  Date column     [ Date ▾ ]                                 │
│  Weight column   [ Weight ▾ ]                               │
│  Notes column    [ Comments ▾ ]  (optional — pick "None")  │
│                                                             │
│  Sheet units     (•) Metric (kg)  ( ) Imperial (lbs)       │
│                                                             │
│  [  Next  ]                                                 │
└─────────────────────────────────────────────────────────────┘
```

- Dropdowns populated from detected header row
- "None" option for Notes column
- Sheet units toggle (Metric / Imperial) — conversion applied during import

### Import Wizard — Step 4: Confirm (Warning Dialog)

A full-page confirmation (NOT a system alert) with Copper accent on the warning icon:

```
┌─────────────────────────────────────────────────────────────┐
│  ⚠  BEFORE YOU CONTINUE                                     │
│                                                             │
│  This import will permanently change your data:            │
│                                                             │
│  • Your goal period will be updated to                     │
│      [FirstDate detected] → [LastDate detected]            │
│      ([N] days total)                                      │
│                                                             │
│  • Your starting weight will be updated to                 │
│      [FirstEntryWeight in user's unit]                     │
│                                                             │
│  • Your ideal weight line will be recalculated             │
│      from scratch across the new period                    │
│                                                             │
│  • Existing app entries for dates where the sheet          │
│      has a valid (non-zero) weight will be overwritten     │
│                                                             │
│  Your target weight ([TargetWeight in user's unit])        │
│  is NOT changed.                                           │
│                                                             │
│  [  Cancel  ]         [  Confirm Import  ]                 │
└─────────────────────────────────────────────────────────────┘
```

The confirmation button is Copper. The cancel button is Nickel-text on transparent background.

### Import Wizard — Step 5: Progress & Summary

**During import:**
```
Importing your data...  [████████░░░░░░░]  47%
```

**After completion:**
```
┌─────────────────────────────────────────────────────────────┐
│  IMPORT COMPLETE                                            │
│                                                             │
│  ✓  142  rows imported                                     │
│  ─   18  rows skipped (no weight recorded)                  │
│  ✗    3  rows failed (unparseable date or weight)           │
│                                                             │
│  Your ideal path has been recalculated.                    │
│  You can now view your history in the Trends tab.          │
│                                                             │
│  [  Done  ]                                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## EF Core Migration

**New migration:** `Add_GoalDates_To_UserProfile`

Changes to `UserProfiles` table:
```sql
ALTER TABLE UserProfiles ADD COLUMN GoalStartDate TEXT;   -- nullable DateOnly (yyyy-MM-dd)
ALTER TABLE UserProfiles ADD COLUMN GoalEndDate   TEXT;   -- nullable DateOnly (yyyy-MM-dd)
```

No existing tables are altered beyond `UserProfiles`. No data loss.

---

## Implementation Steps (TDD Workflow)

### Step 0 — Prerequisites
- [ ] Add `Google.Apis.Sheets.v4`, `Google.Apis.Drive.v3`, `Google.Apis.Auth` to `LeanAI.Infrastructure.csproj`
- [ ] Confirm `AndroidManifest.xml` OAuth redirect URI intent filter is in place

### Step 1 — Domain & Interface Changes (RED → GREEN)
1. Extend `UserProfile` with `GoalStartDate` / `GoalEndDate`
2. Add `InsertBatchAsync` to `IDailyActualWeightRepository`
3. Create `IGoogleSheetsService` + DTOs in Application layer
4. Extend `GenerateIdealPathCommand` with `ExactTotalDays`
5. Write `GenerateIdealPathCommandHandler` test for the override path

### Step 2 — Application Commands (RED → GREEN, each handler TDD)
| Test class | Key branches |
|------------|-------------|
| `UpdateGoalFromImportCommandHandlerTests` | profile found / not found; fields updated correctly |
| `ImportGoogleSheetsCommandHandlerTests` | new entry inserted; existing entry with valid weight updated; existing entry with zero/null weight preserved; unit conversion applied; ideal path regenerated with correct ExactTotalDays; ImportSummaryDto counts correct |

### Step 3 — Application Queries (RED → GREEN)
| Test class | Key branches |
|------------|-------------|
| `GetGoogleSpreadsheetsQueryHandlerTests` | returns mapped list from service |
| `GetSheetColumnsQueryHandlerTests` | returns headers from service |

### Step 4 — Infrastructure Implementation
- `DailyActualWeightRepository.InsertBatchAsync` (EF Core `AddRangeAsync` + `SaveChangesAsync`)
- `GoogleSheetsService` implementation
- `GoogleOAuthService` implementation
- Register all new services in `DependencyInjection.cs`

### Step 5 — EF Core Migration
```
dotnet ef migrations add Add_GoalDates_To_UserProfile --project src/LeanAI.Infrastructure --startup-project src/LeanAI.Maui
dotnet ef database update
```
Verify: 0 errors, existing `UserProfiles`, `DailyIdealWeights`, `DailyActualWeights` rows intact.

### Step 6 — Presentation Layer
- `ImportWizardViewModel` with step state machine
- `ImportWizardPage.xaml` (5-step layout, Industrial Copper palette)
- Settings page additions: "Import from Google Sheets" row + "Disconnect Google" row
- Wire up `SettingsViewModel` commands

### Step 7 — Build & Test
```
dotnet build   → 0 errors, 0 warnings
dotnet test    → all tests green
```

---

## Definition of Done

- [ ] `UserProfile.GoalStartDate` and `UserProfile.GoalEndDate` are persisted in SQLite after import
- [ ] `UserProfile.StartingWeightKg` is updated to the first imported entry's weight
- [ ] `UserProfile.TargetWeightKg` is unchanged after import
- [ ] Ideal weight line is recalculated from first-to-last sheet date using `ExactTotalDays`
- [ ] Existing app entries for dates with null/zero imported weight are preserved
- [ ] Existing app entries for dates with valid (> 0) imported weight are overwritten
- [ ] New entries are inserted for dates with no prior app record
- [ ] Pre-import confirmation page is shown and lists all consequences correctly
- [ ] OAuth refresh token is stored in SecureStorage; subsequent imports skip the sign-in step
- [ ] "Disconnect Google" row clears the stored token
- [ ] Progress bar is shown during import; summary (imported / skipped / failed counts) shown on completion
- [ ] EF Core migration applies cleanly — no data loss to existing tables
- [ ] `ImportGoogleSheetsCommandHandler`, `UpdateGoalFromImportCommandHandler`, `GetGoogleSpreadsheetsQueryHandler`, `GetSheetColumnsQueryHandler` all tested at 100% branch coverage
- [ ] `dotnet build` → 0 errors, 0 warnings. `dotnet test` → all tests green
