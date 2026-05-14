# LeanAI: Functional Requirements & Development Roadmap

## Phase 1: Onboarding & Persistence (The Foundation)
- **Goal:** Establish the user profile and persistent storage.
- **Functional Requirements:**
    - **Setup Wizard:** Mandatory launch if profile is incomplete.
    - **Unit Preference:** Toggle between Metric (kg/cm) and Imperial (lb/in) globally.
    - **User Stats:** Collect Gender (Enum), Age (Int), Height (Double), Starting Weight (Double).
    - **Goal Selection:** Target Weight (Double) and Target Period (Enum: 3mo, 6mo, 1yr).
- **Technical Specs:**
    - Initialize .NET 10 MAUI Shell with EF Core SQLite provider.
    - Implement "Preserve over Purge" data policy (User data survives app updates).
- **Definition of Done (DoD):** App launches, Wizard saves complete profile to SQLite, and the solution is "Runnable" on Android.

## Phase 1.5: Settings Screen (The Control Panel)
- **Goal:** Provide a functional Settings screen accessible from the bottom nav bar, starting with the ability to reset the user profile and re-run the Setup Wizard.
- **Functional Requirements:**
    - **Gear Icon Tab:** The Settings tab in the bottom tab bar displays a gear icon. The icon renders in its active (Copper) state when the Settings screen is the current tab, and in its inactive (Nickel) state otherwise. This replaces any generic placeholder tab icon.
    - **Re-Run the Setup Action:** The Settings screen contains a tappable action row with:
        - **Primary label:** "Re-Run the Setup"
        - **Subtitle:** "Your stats and your goals will be deleted!"
        - Subtitle is styled in Nickel (#9A9EAB) to communicate destructive intent without using a separate warning color.
    - **Reset Flow:** When the action row is tapped:
        1. The existing `UserProfile` record is deleted from SQLite.
        2. The Setup Wizard is pushed modally over the Shell (identical to first-launch behavior).
        3. On wizard completion the new profile is saved; the wizard is dismissed and the Shell resumes.
- **Technical Specs:**
    - Add `DeleteUserProfileCommand` / `DeleteUserProfileCommandHandler` in the Application layer (MediatR `IRequest<Unit>`). Handler calls a new `DeleteAsync` method on `IUserProfileRepository` (deletes the single row if it exists).
    - `SettingsViewModel` (CommunityToolkit.Mvvm) exposes a `[RelayCommand] Task ReRunSetupAsync()` that: (a) sends `DeleteUserProfileCommand`, (b) resolves `WizardPage` from `IServiceProvider`, (c) pushes it modally via `Shell.Current.Navigation`.
    - Active/inactive tab icon state is achieved by supplying two icon assets (`settings_tab.png` for inactive, `settings_tab_active.png` for active) and binding the Shell tab's icon to the active state via `Shell.TabBarSelectedColor` / `ShellContent` properties, or by using two SVG assets with the correct fill colors baked in and wired to MAUI Shell's selected-tab icon override.
    - No new DB migration is required — `DeleteAsync` operates on the existing `UserProfiles` table.
- **DoD:**
    - Settings tab gear icon is Copper when active, Nickel when inactive.
    - Tapping "Re-Run the Setup" deletes the profile and launches the Wizard.
    - Completing the wizard from the re-run saves the new profile and returns to the Shell.
    - All existing Application-layer tests still pass; new `DeleteUserProfileCommandHandler` tests added with 100% branch coverage.
    - `dotnet build` → 0 errors, 0 warnings. `dotnet test` → all tests green.

## Phase 2: AI Safety Validation (The Guardrails)
- **Goal:** Use Gemini to validate the feasibility and safety of the user's goal.
- **Functional Requirements:**
    - **Validation Prompt:** "User: [Stats]. Goal: [Target] in [Period]. Provide safety information for this weight loss according to your best knowledge."
    - **Response Mapping:** Categorize AI output into 'Safe', 'Warning', or 'Danger'. 
    - **User UX:** Present the AI's explanation. Require a "Medical Disclaimer" acknowledgement if the status is 'Danger'.
- **Technical Specs:**
    - Secure storage of Gemini API Key in `SecureStorage`.
    - Infrastructure service for LLM communication using `Microsoft.Extensions.AI`.
- **DoD:** Wizard successfully blocks/warns users based on AI feedback before finalizing the goal.

## Phase 3: The Ideal Path (The Algorithm)
- **Goal:** Generate the mathematical daily trajectory for the chosen period.
- **Functional Requirements:**
    - **Daily Delta Logic:** Calculate $DailyLoss = \frac{StartWeight - TargetWeight}{TotalDays}$.
    - **Path Generation:** Populate `DailyIdealWeight` table for every date in the period.
    - **Goal Reset Logic:** If the user changes goals, delete the `IdealWeight` entries but PRESERVE all existing `ActualWeight` user logs.
- **Technical Specs:** 
    - Background task for batch DB insertion using SQLite transactions.
- **DoD:** Upon wizard completion, the DB contains a full "Ideal" weight map for the selected timeframe without blocking the UI.

## Phase 3.5: AI Configuration Settings (The Control Panel — AI)
- **Goal:** Allow users to view and update the AI model name and Gemini API key directly from the Settings screen, without requiring a reinstall or developer intervention.
- **Functional Requirements:**
    - **AI Model Field:** A text entry labeled "AI Model" in the Settings screen.
        - Default value: `gemini-2.5-flash`.
        - Loaded from the database on screen open.
        - Changes are persisted to the database when the user saves.
    - **Gemini API Key Field:** A masked (password-style) text entry labeled "Gemini API Key".
        - Loaded from SecureStorage on screen open.
        - The key is never written to the database — SecureStorage only.
        - Changes are persisted to SecureStorage when the user saves.
    - **Save AI Settings Action:** A tappable "Save AI Settings" button persists both fields simultaneously.
    - **Hot Reload:** After saving, all subsequent AI calls within the same session use the updated model name and API key — no restart required.
- **Technical Specs:**
    - **`AppSettings` Entity** (Domain layer): New entity extending `BaseEntity` with a single persisted property `string GeminiModelName` (default `"gemini-2.5-flash"`). The API key is never a field on this entity.
    - **`IAppSettingsRepository`** (Domain): Interface with `GetAsync` and `SaveAsync` methods, mirroring `IUserProfileRepository`.
    - **`IApiKeyStorage`** (Domain): New interface abstracting platform-level secure key storage; decouples the Application layer from MAUI `SecureStorage`.
    - **`AppSettingsRepository`** (Infrastructure): EF Core implementation of `IAppSettingsRepository` (single-row pattern identical to `UserProfileRepository`).
    - **`SecureStorageApiKeyStorage`** (Infrastructure): MAUI `SecureStorage` implementation of `IApiKeyStorage`. On `SetAsync`, also updates `GeminiKeyHolder` to hot-reload the active key.
    - **`GeminiKeyHolder`** (Infrastructure): Extend to also carry `GeminiModelName`. Update the lazy `IChatClient` factory to read model name from the holder at creation time.
    - **`AppSettingsDto`** (Application): Record with `string GeminiModelName` and `string GeminiApiKey`.
    - **`GetAppSettingsQuery` / Handler** (Application): Returns `AppSettingsDto` — model name from `IAppSettingsRepository`; key from `IApiKeyStorage`.
    - **`SaveAppSettingsCommand` / Handler** (Application): Accepts `GeminiModelName` and `GeminiApiKey`. Saves model name via `IAppSettingsRepository`; saves key via `IApiKeyStorage`.
    - **`SettingsViewModel`** (Presentation): Loads settings via `GetAppSettingsQuery` on navigation (`OnAppearing`). Exposes `[ObservableProperty] string GeminiModelName` and `[ObservableProperty] string GeminiApiKey`. Exposes `[RelayCommand] Task SaveAiSettingsAsync()` dispatching `SaveAppSettingsCommand`.
    - **`SettingsPage.xaml`**: Add an "AI Configuration" section below the existing "Re-Run the Setup" row. Section contains two `Entry` controls (model name plain text, API key with `IsPassword="True"`) and a "Save AI Settings" tappable row or button.
    - **App Startup**: Extend `App.xaml.cs` provisioning logic to also load `GeminiModelName` from `IAppSettingsRepository` (default if no row exists) and push it into `GeminiKeyHolder` before the first AI call.
    - **EF Core Migration**: Add `AppSettings` table (`Id` Guid PK, `GeminiModelName` string NOT NULL). No existing tables are altered.
    - Register `IAppSettingsRepository`, `IApiKeyStorage`, and `AppSettings`-related services in `DependencyInjection.cs`.
- **DoD:**
    - Settings screen displays the current AI model name and masked API key on open.
    - "Save AI Settings" persists model name to the `AppSettings` DB table and key to SecureStorage.
    - AI calls following a save use the new model/key without restarting the app.
    - EF Core migration applies cleanly — no data loss to `UserProfiles`, `DailyIdealWeights`, or `DailyActualWeights`.
    - `GetAppSettingsQueryHandler` and `SaveAppSettingsCommandHandler` tested at 100% branch coverage.
    - `dotnet build` → 0 errors, 0 warnings. `dotnet test` → all tests green.

## Phase 4: Daily Tracking & Feedback (The Habit) ✅ COMPLETE
- **Goal:** Provide a seamless entry point for daily weight recording.
- **Functional Requirements:**
    - **Autoload:** Automatically open the entry screen if today's data is missing.
    - **Inputs:** Daily Weight (Decimal) and Optional Comments (Text).
    - **Real-time Feedback:**
        - **Yesterday Delta:** Visual indicator of change since the last entry.
        - **Ideal Warning:** Highly visible warning if `ActualWeight > IdealWeight[Today]`.
        - **Weekly Prediction:** Display predicted weekly loss vs. ideal weekly loss.
- **Technical Specs:** Implementation of the "Autosave" pattern (see UI_UX_SPEC.md).
- **DoD:** User can record weight and see immediate color-coded comparisons against their ideal plan.

# Phase 4.5: External Data Integration (Google Sheets Import) ✅ COMPLETE

**Goal**
Enable users to migrate historical weight data from Google Sheets to LeanAI to provide immediate longitudinal insights. After import the ideal weight line is automatically recalculated to span the full date range found in the sheet — including future-dated rows — giving the user immediate longitudinal context.

**Functional Requirements (as implemented)**
- **Authentication:** OAuth 2.0 Authorization Code flow with PKCE via `WebAuthenticator`. Refresh token stored in `SecureStorage`; subsequent imports use silent refresh (no re-authentication required).
- **Permission Scope:** `spreadsheets.readonly` + `drive.readonly`.
- **Token Management:** "Disconnect Google Account" row in Settings clears the stored token.
- **Source Selection:** 5-step modal wizard; Step 2 lists the user's Google Drive spreadsheets by name.
- **Data Mapping:** User-driven column mapping via dropdowns (Date, Weight, Notes). Auto-selects sensible defaults by matching header names case-insensitively. Toggle for Metric / Imperial sheet units.
- **Import row cap:** Maximum 400 rows read per import (`"2:401"` range).
- **Date parsing:** Handles 16+ explicit formats plus a `DateTime.TryParse` fallback covering locale-specific strings returned by the Google Sheets API.
- **Two-phase import:**
    - **Phase 1 — Date range / ideal path:** All rows with a valid date (including future rows with no weight yet) define the goal period. `GoalStartDate` = first date with a recorded weight; `GoalEndDate` = last date in the sheet regardless of whether a weight is recorded. Ideal weight line is regenerated across this full span.
    - **Phase 2 — Actual weights:** Only rows where the weight cell is non-null and > 0 are written to `DailyActualWeight`. Rows with a valid date but no weight are counted but not written.
- **Overwrite rule:** An existing `DailyActualWeight` entry is overwritten only when the imported weight is > 0. Zero or null → existing app entry is preserved.
- **Pre-import warning:** Explicit full-page confirmation (Step 4) lists every consequence before any data is written.
- **Import summary (Step 5):**
    - Dates found in spreadsheet
    - Days on ideal weight line (recalculated span)
    - Dates with a recorded weight (found in sheet)
    - Weight entries saved to database
    - Parse failures (shown only when > 0)

**Technical Specifications**
- **OAuth client type:** Android application type in Google Cloud Console (Web clients do not support custom URI schemes).
- **PKCE:** SHA256 code challenge; no client secret required.
- **`IGoogleTokenStorage`:** Application-layer interface abstracting `SecureStorage`; implemented by `SecureGoogleTokenStorage` in the MAUI layer (mirrors the `IApiKeyStorage` pattern).
- **`IGoogleSheetsService`:** Defined in Application layer; implemented by `GoogleSheetsService` in Infrastructure using `Google.Apis.Sheets.v4` + `Google.Apis.Drive.v3`.
- **`GenerateIdealPathCommand`:** Extended with optional `ExactTotalDays` parameter; when set, overrides `TargetPeriod.TotalDays()`. Fully backward-compatible.
- **`UpdateGoalFromImportCommand`:** Sets `GoalStartDate`, `GoalEndDate`, and `StartingWeightKg` on `UserProfile`.
- **EF Core migration `Add_GoalDates_To_UserProfile`:** Adds nullable `GoalStartDate` and `GoalEndDate` TEXT columns to `UserProfiles`.
- **New converters:** `IntEqualsConverter`, `IntGreaterThanZeroConverter`, `StringNotEmptyConverter`.

**Definition of Done**
- ✅ User authenticates with Google; refresh token persisted; subsequent imports skip sign-in.
- ✅ Spreadsheet list fetched and displayed; user selects one.
- ✅ Column mapping dropdowns populated from sheet header row with sensible auto-defaults.
- ✅ Pre-import confirmation page shown before any data is written.
- ✅ All dated rows (including future rows) define the goal period and ideal weight span.
- ✅ Only rows with weight > 0 are written to `DailyActualWeight`.
- ✅ Existing entries with valid weights are overwritten; entries with zero/null imported weight are preserved.
- ✅ `UserProfile.GoalStartDate`, `GoalEndDate`, `StartingWeightKg` updated; `TargetWeightKg` unchanged.
- ✅ Ideal weight line regenerated across the full sheet date range.
- ✅ 4-metric import summary displayed on completion.
- ✅ "Disconnect Google" clears stored token.
- ✅ All 79 tests pass; `dotnet build` → 0 errors, 0 warnings.

## Phase 5: The Trends Screen ✅ COMPLETE

- **Goal:** High-level visualization of weight evolution and weekly trends.
- **Scope decision:** Calendar Achievement Matrix deferred to a future phase. Phase 5 delivers the Trends tab only (charts-only screen).

### As-Implemented Functional Requirements

**Trends tab** (existing Shell tab, replaced placeholder):

- **Weight Evolution chart** — fills the upper portion of the screen, title "Actual weight vs. Ideal Weight":
    - **Nickel line** (`#9A9EAB`): Daily ideal weight — continuous line across the full goal period.
    - **Copper line** (`#D28B5C`): Actual recorded weights — line segments connecting consecutive calendar dates only; gaps in recording are visible as empty space.
    - **White dots**: Ideal weekly averages — plotted at each Monday + GoalEndDate.
    - **Amber dots** (`#E8A838`): Actual weekly averages — plotted at each Monday + GoalEndDate where ≥1 actual weight was recorded in that Mon–Sun window.
    - X-axis: date range from `GoalStartDate` to `GoalEndDate`; month-boundary labels.
    - Y-axis: auto-scaled to the min/max of ideal + actual data with 5% padding; 5 labeled grid lines.

- **Weekly Loss/Gain chart** — below the evolution chart, title "Weekly average weight loss":
    - One bar per week anchor where at least one previous week also has data (i.e., `N` weekly averages → `N−1` bars).
    - Bar value = `avg(prevWeek) − avg(thisWeek)`. Positive = lost weight = copper bar. Negative = gained weight = nickel bar.
    - X-axis labels: "d MMM" format at the Monday anchor of each week.

- **Weekly average definition:** Mean of all actual weight entries recorded Mon–Sun of that calendar week. Anchor dates = every Monday in range + GoalEndDate (partial last week always included).

- **Empty state:** When no ideal path data exists, both charts are hidden and a message is shown.

- **Screen layout:** Both charts fill the full screen (no scroll). Evolution chart takes all remaining space via `Height="*"`; bar chart is fixed height below it.

### Technical Decisions

| Decision | Detail |
|----------|--------|
| Evolution chart rendering | Custom MAUI `GraphicsView` + `WeightEvolutionDrawable` (`IDrawable`) — Microcharts does not support 4 synchronized series on a shared coordinate system |
| Bar chart rendering | `Microcharts.Maui` 1.0.1 `BarChart` — per-entry colour, satisfies Microcharts integration requirement |
| `GoalStartDate` invariant fix | `GenerateIdealPathCommandHandler` now writes `GoalStartDate` / `GoalEndDate` to `UserProfile` after ideal path generation — manual-setup users previously had these null |
| New repository method | `IDailyIdealWeightRepository.GetRangeAsync(DateOnly from, DateOnly to)` added |
| Weekly average | Mean of all Mon–Sun entries; no minimum recording count required |
| Weekly delta | `avg(prevWeek) − avg(thisWeek)`; weeks with no recordings are excluded from both average and delta series |
| Partial last week | `GoalEndDate` is always appended as a final weekly anchor, even when mid-week |

- **DoD:** ✅ User can see the full weight evolution against the ideal line and identify weekly loss/gain trends. 95/95 tests passing.

---

## Phase 6: The History Hub (Calendar & Engagement) ✅ COMPLETE

### Goal
Provide a high-density, interactive historical overview of user progress through a gamified calendar interface to drive long-term engagement.

### Functional Requirements

#### Calendar View
- A new screen accessible from the bottom menu tab.
- A standard monthly grid with navigational controls (Previous / Next Month).
- **Date boundary:** Calendar spans `GoalStartDate` to `GoalEndDate` only. Days outside this range are dimmed and non-clickable; navigation must not allow access to data entry for those days.
- **Month continuity:** Weeks flow across month boundaries — e.g., if 30 April falls on a Thursday, the same row continues with Friday 1 May.
- **First-day-of-week setting (Settings screen):** User can choose Sunday or Monday as the first column of the weekly grid. This setting affects calendar display only.

#### Interactive Cells
- Past dates within the goal date range (on or before today) are always tappable, whether or not a record exists.
- Future dates within the goal date range (after today) are **not** tappable.
- Tapping opens the data-entry screen (`LogPage`) pushed modally for that date:
    - Pre-populated with existing data if a weight record exists for that day.
    - Opens in new-entry mode if no record exists.

#### Achievement Matrix — Visual Logic

| State | Visual | Clickable |
|-------|--------|-----------|
| Day outside goal range | Dimmed Nickel (35% opacity), no halo | No |
| Future day within goal range | Dimmed Nickel (35% opacity), no halo | No |
| Past day within goal range, no record | Dimmed Nickel, no halo | Yes — opens new-entry mode |
| Past day within goal range, has record | Active; halo + coloured day number | Yes — opens pre-populated entry |

**Daily Delta (Halo):**
- **Copper circle halo:** Today's weight is *lower* than the previous recorded day's weight (progress), **or** this is the very first recorded day (optimistic default — no prior day to compare).
- **Nickel circle halo:** Today's weight is *higher than or equal to* the previous recorded day's weight (stagnation / gain).

**Weekly Trend (Text colour):**
- **Copper text:** The current calendar week's average weight is *lower* than the previous week's average (positive trend).
- **Nickel text:** The current calendar week's average weight is *higher than or equal to* the previous week's average (negative trend).

### Technical Specifications
- **View component:** MAUI `CollectionView` (`GridItemsLayout`, Span=7) for calendar cells.
- **State management:** `ObservableCollection<CalendarDayViewModel>` drives UI states (colours, halo, clickability) computed from SQLite data via `GetCalendarMonthQuery`.
- **Weekly averages persistence:** New `WeeklyAverages` DB table (keyed on the Monday of each Mon–Sun week). Updated in two places:
    1. `UpsertDailyLogCommandHandler` — after every daily weight save (including historical edits via the calendar).
    2. `ImportGoogleSheetsCommandHandler` Phase 3 — after the batch import, all written entries are grouped by their Mon–Sun Monday, and a weekly average is computed and upserted for every affected week. This ensures the calendar is immediately populated with trend data after an import without requiring a separate recalculation step.
- **`CalendarFirstDay` setting:** Stored in the `AppSettings` table. Default = Monday. Affects column header order only; does not change how averages are computed.
- **LogPage reuse:** Pushed modally from `CalendarViewModel` (same pattern as the Wizard). `LogViewModel` gains a `LoadForDateAsync(DateOnly)` entry point for modal use.
- **Tab order:** Log | Trends | **Calendar** | Settings. Calendar icon = calendar-related SVG asset.
- **Calendar cell sizing:** Cells fill the available vertical space dynamically. `CalendarViewModel` exposes a `CellHeight` observable property updated via `CalendarPage`'s `SizeChanged` event on the grid `CollectionView`. The number of week rows (always `Days.Count / 7`) is tracked after each month load; `CellHeight = availableGridHeight / weekRowCount`. Day-number label `FontSize=20` (bold); halo ellipse `50×50`. This ensures the grid fills the screen on all device sizes without hardcoded heights.
- **UI palette:**
    - Background: Deep brushed charcoal / black (`ColorBase`).
    - Positive trend: Luminous Copper (`#D28B5C`) for halos and text.
    - Stagnation / gain: Matte Nickel (`#9A9EAB`) for halos and text.
    - No data / out-of-range: Dimmed Nickel with reduced opacity (35%).

### Definition of Done
- ✅ Users can navigate between months and see accurately colour-coded achievement markers for each recorded day.
- ✅ Clicking a valid date with existing data opens the pre-populated log entry.
- ✅ Clicking a valid date with no data opens the log entry in new-entry mode.
- ✅ Days outside `GoalStartDate`–`GoalEndDate` are dimmed and tapping them does nothing.
- ✅ Future in-range dates are dimmed and non-clickable.
- ✅ First-day-of-week setting (Sunday / Monday) is available in Settings and correctly reorders calendar columns.
- ✅ Calendar cells fill the available screen height dynamically — no hardcoded row heights.
- ✅ Weekly averages are calculated and persisted after Google Sheets import.
- ✅ `dotnet build` → 0 errors, 0 warnings. `dotnet test` → all 123 tests green.