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

---

## Phase 7: UI/UX Improvements ✅ COMPLETE

### Goal
Polish all four main screens for readability and usability: larger fonts and better layout on the Log screen; an interactive zoom on the Trends chart; bigger tap targets on the Calendar navigation; auto-saving Settings with a cleaner section structure; and an updated Android launcher icon.

---

### Log Screen

**Header**
- "Daily Entry" span: `FontSize` 16 → 24 (Bold, unchanged).
- Date span (`EntryDateLabel`): `FontSize` 16 → 20; colored **Copper** to improve visibility.

**Card section labels**
- "Current Weight" and "Comments (Optional)" labels: `FontSize` 13 → 15.

**Spacing**
- Extra top margin (24 pt) before the "REAL-TIME ANALYSIS" section label to create clear visual separation between the data-entry cards and the display-only analysis area.

**Analysis section header**
- "REAL-TIME ANALYSIS" label: `FontSize` 11 → 13.

**Analysis cards**
- Sub-title labels ("Yesterday Delta", etc.): `FontSize` 11 → 13.
- Value labels (e.g., Yesterday Delta value): `FontSize` 22 → 26.
- Sub-row labels inside Weekly Loss card ("Current", "Ideal"): `FontSize` 11 → 13.
- Sub-row value labels: `FontSize` 15 → 18.

**Weekly Loss card — rename**
- Title changes from "Predicted Weekly Loss" to:
  ```
  Weekly Loss for week starting on:
  [Monday date of the current week, formatted "d MMM yyyy"]
  ```
- Both "Current" (actual projected rate) and "Ideal" (target rate) sub-rows are retained.
- The week-start date is derived from the current entry date (already known in the handler); it is added to `LogContextDto` as `DateOnly WeekStartDate`.

**New "Current Average Weekly Weight" card**
- Full-width card below the 2-column analysis grid.
- Title: "Current Average Weekly Weight" (Nickel, FontSize 13).
- Value: formatted weight + unit (e.g., "87.3 kg"), FontSize 26, Bold.
- **Color rule:** Copper if the current calendar week's running average weight is strictly less than the previous calendar week's average (positive trend = losing weight); Nickel otherwise.
- Requires two new fields in `LogContextDto`: `double? CurrentWeekAverageWeightKg` and `double? LastWeekAverageWeightKg`.
- `GetLogContextQueryHandler` adds one extra `GetRangeAsync` call for the prior Mon–Sun window to compute `LastWeekAverageWeightKg`.

**Remove debounce hint**
- The label "Data is debounced and saved automatically" is removed from the screen entirely.

---

### Trends Screen — Evolution Chart Zoom

**Behavior**
- Tapping the evolution chart once activates **3-month zoom mode**:
  - Window: `(today − 76 days)` to `(today + 14 days)` — approximately 2 months and 2 weeks before today, 2 weeks after.
  - Y-axis auto-rescales to data within the window only for improved readability.
  - Chart title changes to: "Actual weight vs. Ideal Weight  (3-month view)".
- Tapping again **restores full mode**:
  - Window: `GoalStartDate` to `GoalEndDate`.
  - Title reverts to: "Actual weight vs. Ideal Weight".
- The **Weekly average loss bar chart** (bottom row) is completely unaffected by the zoom toggle.

**Technical approach**
- `WeightEvolutionDrawable` gains `IsZoomed`, `ZoomFrom`, `ZoomTo` properties.
- `DateToX` uses the effective date range (zoom or full) internally.
- `TrendsViewModel` exposes `EvolutionChartTitle` (`[ObservableProperty]`) and a `ToggleZoom()` method.
- `TrendsPage` wires a `TapGestureRecognizer` on the `GraphicsView`; the code-behind handler calls `ViewModel.ToggleZoom()` then `EvolutionView.Invalidate()`.

---

### Calendar Screen — Navigation Tap Targets

- The Previous Month ("‹") and Next Month ("›") controls are replaced with large transparent `Grid` cells (`Padding="20,16"`) containing the glyph as a `Label`.
- Column widths increase from 44 → 72 px to accommodate the larger touch zones.
- A `TapGestureRecognizer` bound to `PreviousMonthCommand` / `NextMonthCommand` replaces the `ImageButton.Command` binding.

---

### Settings Screen — Reorganization & Auto-save

**New section order**
1. **GENERIC SETTINGS** — First Day of Week (Picker)
2. **AI CONFIGURATION** — AI Model (text), Gemini API Key (password text)
3. **DATA IMPORT** — Import from Google Sheets + Disconnect Google (conditional)
4. **PROFILE** — Re-Run the Setup

**Auto-save**
- **Picker (`First Day of Week`)**: saves immediately on `SelectedIndexChanged`.
- **Text entries (`AI Model`, `API Key`)**: debounced 500 ms (same pattern as `LogViewModel`).
- **Save button**: removed from the screen entirely.

**`SettingsViewModel` changes**
- Remove: `HasChanges`, `SaveAiSettingsCommand`, `[NotifyCanExecuteChangedFor]`, `RefreshHasChanges()`.
- Add: `CancellationTokenSource? _saveCts`, `TriggerSave()`, `SaveWithDebounceAsync()`, `SaveImmediateAsync()`.
- `OnCalendarFirstDayIndexChanged` → calls `SaveImmediateAsync()`.
- `OnGeminiModelNameChanged` / `OnGeminiApiKeyChanged` → calls `TriggerSave()`.

---

### Android Launcher Icon

- All `mipmap-*` density folders (mdpi, hdpi, xhdpi, xxhdpi, xxxhdpi) from `z-com-ai/icontoolkit/android/res/` are copied into `src/LeanAI.Maui/Platforms/Android/Resources/`, replacing any previously generated launcher icons.
- The adaptive icon descriptor `mipmap-anydpi-v26/ic_launcher.xml` is included.

---

### Technical Specifications Summary

| Area | Change |
|------|--------|
| `LogContextDto` | Add `DateOnly WeekStartDate`, `double? CurrentWeekAverageWeightKg`, `double? LastWeekAverageWeightKg` |
| `GetLogContextQueryHandler` | Compute new DTO fields; add one extra `GetRangeAsync` call for prior-week entries |
| `LogViewModel` | Add `WeekStartDateLabel`, `CurrentWeeklyAverageText`, `IsWeeklyAverageTrending` observable properties |
| `WeightEvolutionDrawable` | Add `IsZoomed`, `ZoomFrom`, `ZoomTo`; update `DateToX` and `Draw` to respect zoom window |
| `TrendsViewModel` | Add `EvolutionChartTitle` property; add `ToggleZoom()` method |
| `TrendsPage` | Wire tap gesture on `GraphicsView`; invalidate on toggle |
| `CalendarPage.xaml` | Replace `ImageButton` arrow controls with large-tap `Grid`+`Label` controls |
| `SettingsViewModel` | Remove explicit save; add debounce auto-save pattern |
| `SettingsPage.xaml` | Reorder sections; remove Save button |
| Android Resources | Copy mipmap folders from toolkit |

---

### Definition of Done

- ✅ Log screen header "Daily Entry" and date are visibly larger; date is Copper-colored.
- ✅ All Log screen analysis fonts enlarged as specified.
- ✅ Weekly Loss card retitled with week-start date; both Current and Ideal sub-rows retained.
- ✅ "Current Average Weekly Weight" card present; Copper when this week's average < last week's; Nickel otherwise.
- ✅ "Data is debounced and saved automatically" text removed.
- ✅ Trends: single tap on evolution chart toggles 3-month zoom; second tap restores full view; bar chart unaffected; title reflects state.
- ✅ Calendar: Previous / Next month arrows have a 72-px-wide, full-padding tap zone.
- ✅ Settings sections appear in order: GENERIC SETTINGS → AI CONFIGURATION → DATA IMPORT → PROFILE.
- ✅ Settings: all fields auto-save (Picker immediately; text entries debounced 500 ms); Save button absent.
- ✅ Android launcher icon updated with assets from toolkit (`play_store_512.png` replaces source icon; MAUI generates mipmap densities at build time).
- ✅ `dotnet test` → 127 tests, 0 failures.
- ✅ `dotnet build` → 0 errors, 0 warnings (pre-existing SkiaSharp XA0141 warning unrelated to Phase 7).

---

## Phase 8: Third-Party Integration — Import Run via Share Sheet ✅ COMPLETE

### Goal
Allow the user to share a screenshot of a run from any third-party activity tracking app (Strava, Nike Run Club, Garmin, etc.) directly into LeanAI via the Android Share Sheet. LeanAI extracts the distance, pace, and duration using Gemini Vision, stores the metrics in the database, and appends a human-readable summary to today's weight log comment.

---

### Functional Requirements

#### Android Share Sheet Entry Point
- The app registers an Android Activity named **"Import Run"** that appears in the Android Share Sheet whenever the user shares an image (`image/*` MIME type).
- The Activity is a thin proxy: it reads the image from the share intent, resolves the MediatR mediator from the DI container, and dispatches `ImportRunCommand`. All business logic lives in the command handler.
- The design is explicitly extensible — future activity types (cycling, swimming) add a new Android Activity with a new label and a new command; no existing code is modified.

#### Image-to-Metrics Extraction (Gemini Vision)
- `ImportRunCommand` carries the raw image bytes, the image MIME type (resolved from the Android content resolver), and today's date.
- The command handler delegates image analysis to `IRunImageAnalysisService` (Application layer interface), implemented in Infrastructure by `GeminiRunImageAnalysisService`.
- The Gemini prompt asks the model to extract exactly three metrics and respond in a strict JSON array format:
  ```json
  [
    { "parameter_name": "distance", "value": 5.2, "unit": "km" },
    { "parameter_name": "pace",     "value": "5:30", "unit": "min/km" },
    { "parameter_name": "duration", "value": "28:30", "unit": "mm:ss" }
  ]
  ```
  - `parameter_name` is an enum: `"distance"`, `"pace"`, or `"duration"`.
  - `value` may be a number or a string (e.g., pace is `"5:30"`).
  - `unit` is a plain string.
- If Gemini returns a response that cannot be parsed as the expected JSON array (missing fields, wrong format, empty array), the service throws an exception indicating extraction failure.

#### Database — `ActivityLogs` Table (new, `ActivityTracking` bounded context)
- A new `ActivityLog` domain entity lives in the `ActivityTracking` bounded context (separate from `WeightManagement`).
- Schema: `Id` (Guid PK), `Date` (DateOnly), `Activity` (string), `ParameterName` (string), `Value` (string), `Unit` (string).
- For each run import, **three rows** are inserted — one per extracted metric (distance, pace, duration).
- `Activity` is always `"run"` for this phase.
- A new EF Core migration adds the `ActivityLogs` table without modifying any existing table.

#### Comment Appending (cross-context via mediator)
- After storing the activity metrics, the handler dispatches `AppendActivityCommentCommand(DateOnly Date, string Comment)` via the mediator (cross-context call into `WeightManagement`).
- The comment text is formatted as:
  ```
  I run for {distance_value}{distance_unit} at a pace of {pace_value}{pace_unit}. Total time: {duration_value}{duration_unit}.
  ```
  Example: `I run for 5.2km at a pace of 5:30min/km. Total time: 28:30.`
- `AppendActivityCommentCommandHandler` behavior:
  - **Entry exists for today:** append the comment to `DailyActualWeight.Notes` (with a newline prefix if Notes is not empty). Weight is never modified.
  - **No entry for today:** create a new `DailyActualWeight` with `WeightKg = 0` and `Notes = comment`.

#### User Feedback (Android Toasts)
- **Success:** A short Android Toast is shown: `"Run imported: {distance_value} {distance_unit} in {duration_value} {duration_unit}."` (e.g., `"Run imported: 5.2 km in 28:30."`). The Activity then finishes.
- **Failure:** A long Android Toast is shown: `"Could not extract run data — please try again."` The Activity then finishes.
- Both Toasts are shown from the `ImportRunActivity` after the command completes or throws.

---

### Technical Specifications

#### New Bounded Context: `ActivityTracking`

**Domain (`LeanAI.Domain/ActivityTracking/`)**
- `ActivityLog` entity extending `BaseEntity`: `DateOnly Date`, `string Activity`, `string ParameterName`, `string Value`, `string Unit`.
- `IActivityLogRepository` interface: `Task AddRangeAsync(IEnumerable<ActivityLog> logs, CancellationToken ct)`.

**Application (`LeanAI.Application/ActivityTracking/`)**
- `ActivityMetricDto` record: `(string ParameterName, string Value, string Unit)`.
- `IRunImageAnalysisService` interface: `Task<IReadOnlyList<ActivityMetricDto>> AnalyzeAsync(byte[] imageBytes, string mimeType, CancellationToken ct)`.
- `ImportRunCommand(byte[] ImageBytes, string MimeType, DateOnly Date)` implementing `IRequest<IReadOnlyList<ActivityMetricDto>>`.
- `ImportRunCommandHandler`: injects `IRunImageAnalysisService`, `IActivityLogRepository`, `IMediator`. Analyzes image → saves 3 `ActivityLog` rows → dispatches `AppendActivityCommentCommand`.

**Application (`LeanAI.Application/WeightManagement/Commands/AppendActivityComment/`)**
- `AppendActivityCommentCommand(DateOnly Date, string Comment)` implementing `IRequest`.
- `AppendActivityCommentCommandHandler`: injects `IDailyActualWeightRepository`. Upserts `DailyActualWeight.Notes` for the given date without touching `WeightKg`.

**Infrastructure (`LeanAI.Infrastructure/ActivityTracking/`)**
- `ActivityLogRepository`: EF Core implementation of `IActivityLogRepository`.
- `GeminiRunImageAnalysisService`: implementation of `IRunImageAnalysisService`. Sends a multimodal chat message (image + text) to Gemini via `IChatClient`. Parses the JSON array response; throws `InvalidOperationException` if parsing fails or the array is empty/incomplete.

**Infrastructure — Database**
- `LeanAIDbContext` gains `DbSet<ActivityLog> ActivityLogs`.
- EF Core migration: `Add_ActivityLogs` — adds `ActivityLogs` table, no existing tables altered.

**Presentation — Android (`LeanAI.Maui/Platforms/Android/`)**
- `ImportRunActivity` extending Android `Activity` (not `MauiAppCompatActivity`).
- `[IntentFilter]` attribute: `ActionSend`, `CategoryDefault`, `DataMimeType = "image/*"`, `Label = "Import Run"`, `Exported = true`.
- On `OnCreate`: reads image URI from `Intent.ExtraStream`, resolves MIME type from `ContentResolver`, reads bytes into `byte[]`, resolves `IMediator` via `IPlatformApplication.Current!.Services`, dispatches `ImportRunCommand` asynchronously via `Task.Run`, shows success or failure Toast via `RunOnUiThread`, calls `Finish()`.
- **Loading screen:** `SetContentView` is called at the start of `OnCreate` with a programmatic layout showing the LeanAI colour palette (dark background `#222222`, copper spinner `#D28B5C`, nickel label `#9A9EAB`) so the user sees a branded spinner instead of a blank white window during the AI call.
- **Fresh-process API key provisioning:** When the share-sheet Activity starts in a fresh process (i.e., the MAUI main window has never opened), `App.xaml.cs`'s `ProvisionAiSettingsAsync()` is never called and `GeminiKeyHolder.ApiKey` remains empty. `ImportRunActivity` must therefore read the key from `SecureStorage` (key name `"gemini_key"`) and set it on the singleton `GeminiKeyHolder` inside `Task.Run`, before the mediator send. This mirrors what `App.xaml.cs` does at window creation time.

**DI Registration**
- `IActivityLogRepository → ActivityLogRepository` (scoped) in `DependencyInjection.cs`.
- `IRunImageAnalysisService → GeminiRunImageAnalysisService` (transient) in `DependencyInjection.cs`.

**Test project**
- `LeanAI.Infrastructure` added as a `<ProjectReference>` in `LeanAI.Tests.csproj` so that `GeminiRunImageAnalysisService.ParseResponse` (marked `internal static`) can be tested directly.
- `[assembly: InternalsVisibleTo("LeanAI.Tests")]` added to `LeanAI.Infrastructure.csproj`.

---

### Definition of Done
- ✅ Android Share Sheet lists "Import Run" when sharing any image from another app.
- ✅ Sharing a valid run screenshot extracts distance, pace, and duration from Gemini and stores 3 `ActivityLog` rows in the DB.
- ✅ Today's `DailyActualWeight.Notes` is updated with the run summary (entry created with `WeightKg = 0` if none existed).
- ✅ Success Toast confirms the imported metrics.
- ✅ Sharing an invalid image shows the failure Toast.
- ✅ `AppendActivityCommentCommandHandler` and `ImportRunCommandHandler` tested at 100% branch coverage.
- ✅ `GeminiRunImageAnalysisService` parse logic tested with valid JSON, malformed JSON, empty array, and missing fields.
- ✅ EF Core migration applies cleanly with no data loss to existing tables.
- ✅ `dotnet build` → 0 errors, 0 warnings. `dotnet test` → all tests green (140 total).

---

## Phase 9: Food Calorie Import — Share Sheet & Daily Tracking ✅ COMPLETE

### Goal
Allow the user to share a meal photo from any app into LeanAI via the Android Share Sheet. Gemini Vision analyses the image and returns a list of food items with their individual caloric values. The user reviews and edits the list (food names and quantities), can request a Gemini re-evaluation of calories, and when satisfied saves the food log for a chosen date. Total daily calories are visible on the Daily Log screen via a tappable tile that opens the same edit screen.

---

### Functional Requirements

#### Android Share Sheet Entry Point — "Import Food"
- The app registers a second Android Activity named **"Import Food"** that appears in the Android Share Sheet whenever an image is shared (`image/*` MIME type).
- The Activity is a thin proxy: reads the image from the share intent, dispatches `AnalyzeFoodImageCommand` via MediatR, stores the result in the singleton `FoodImportStateService`, and launches the main LeanAI app. All business logic lives in the command handler.
- A branded loading screen (same LeanAI palette as Phase 8) is displayed while Gemini processes the image.
- **Success:** LeanAI's main app is brought to the foreground. `LogViewModel.OnAppearing` detects the pending import via `FoodImportStateService` and navigates modally to the Food Review screen.
- **Failure:** A long Android Toast "Could not analyse the meal — please try again." is shown. The Activity finishes without launching the main app.

#### Food Review Screen
- Accessible from two entry points: (1) automatic push after share-sheet import; (2) tapping the **Calories Today** tile on the Daily Log screen.
- **Header:** "Food Review" title followed by an editable `DatePicker`:
  - Default date: today (import mode) or the date tapped in the Daily Log (edit mode).
  - **Minimum date:** `UserProfile.GoalStartDate` (loaded via `GetUserProfileQuery`). Falls back to 1 year ago if `GoalStartDate` is null.
  - **Maximum date:** today (future dates are not allowed).
  - In **edit mode**, changing the date immediately reloads the food log for the newly selected date.
  - In **import mode**, changing the date only changes the save target; the Gemini-extracted items stay fixed.
- Shows a scrollable list where each row displays:
  - **Food item name** — editable `Entry`.
  - **Quantity** — editable `Entry` (free text, e.g. "200 g", "1 cup").
  - **Calories** — read-only label in kcal. Displayed as "—" for newly added rows not yet re-evaluated.
  - **Delete row** button.
- **Total calories summary row** — pinned below the food list (outside the scroll area), shows the running sum of all rows' calorie values. Updates automatically as rows are added, deleted, or re-evaluated. Displays "—" when no calories are known.
- **"Add item"** button — appends a new empty row (name blank, quantity blank, calories "—").
- **"Re-evaluate with AI"** button — sends the full current list (all names + quantities) to Gemini as a text-only request; calories column and total update in place for all rows.
- **"Save"** button — persists all rows to the database for the chosen date (full replace), dismisses the screen, and **navigates the Daily Log to that same date** so the user immediately sees the updated Calories Today tile.
- **"Delete All"** button — deletes all food log entries for the chosen date after a confirmation prompt; Calories Today tile resets to "—".
- **"Cancel"** button (shown in import mode only) — dismisses the screen without saving anything.

#### Gemini Food Analysis
- `AnalyzeFoodImageCommand` sends the meal image to Gemini Vision and requests a JSON array:
  ```json
  [{ "food_item": "string", "quantity": "string", "calories": number }]
  ```
  All three fields are required per item. If the response cannot be parsed or is empty, the service throws `InvalidOperationException`; the Activity shows the failure toast.
- `RecalculateCaloriesCommand` takes the current list as `IReadOnlyList<FoodItemInputDto>` (name + quantity only) and sends a text-only Gemini request asking for the same JSON format with recalculated calories. Used by the "Re-evaluate with AI" button.

#### Database — `FoodTracking` Bounded Context
- New **`CaloryLog`** entity (the central calorie ledger):
  - `Id` (Guid PK), `Date` (DateOnly), `Calories` (double), `SourceType` (string: `"food"` in this phase; extensible to `"activity"` for calorie burn in a future phase).
- New **`FoodLog`** entity (food item details):
  - `Id` (Guid PK), `Date` (DateOnly), `FoodItem` (string), `Quantity` (string), `CaloryLogId` (FK → `CaloryLog`, cascade delete).
- **Relationship:** one `FoodLog` row per food item; each `FoodLog` links to exactly one `CaloryLog` row holding its calorie count. Future activity calorie entries will add their own `CaloryLog` rows without a `FoodLog`.
- **Save strategy:** `SaveFoodLogCommand` performs a full replace — deletes all `FoodLog` rows for the date (cascades to `CaloryLog`), then inserts fresh rows. This handles both initial save and edits.
- A new EF Core migration `Add_FoodTracking` adds `CaloryLogs` and `FoodLogs` tables. No existing tables are altered.

#### Daily Log Screen — Calories Today Tile
- A new full-width tile is added to the **REAL-TIME ANALYSIS** section of the Daily Log screen, positioned between the two-column grid (Yesterday Delta / Weekly Loss) and the existing "Current Average Weekly Weight" card.
- **Title:** "Calories Today" (Nickel, FontSize 13, character spacing 1).
- **Value:** total kcal for today, e.g. `"1 847 kcal"`, FontSize 26, Bold, **Copper** colour. Displays "—" in Nickel if no food log exists for today.
- **Tappable:** navigates modally to the Food Review screen in edit mode for today.

---

### Technical Specifications

| Area | Detail |
|------|--------|
| `AnalyzeFoodImageCommand(byte[], string, DateOnly)` | Returns `IReadOnlyList<FoodItemDto>` — no DB write. `ImportFoodActivity` stores the result in `FoodImportStateService` singleton. |
| `RecalculateCaloriesCommand(IReadOnlyList<FoodItemInputDto>)` | Returns `IReadOnlyList<FoodItemDto>`. Text-only Gemini call (no image required). |
| `SaveFoodLogCommand(DateOnly, IReadOnlyList<FoodItemDto>)` | Full replace for date: delete existing `FoodLog` rows (cascade deletes `CaloryLog`), then insert new rows using EF Core navigation property so `CaloryLog` is created implicitly. |
| `DeleteFoodLogForDateCommand(DateOnly)` | Deletes all `FoodLog` rows for date; cascade delete removes linked `CaloryLog` rows. |
| `GetFoodLogForDateQuery(DateOnly)` | Returns `IReadOnlyList<FoodLogEntryDto>` — eager-loads `CaloryLog` for calorie values. |
| `GetTotalCaloriesForDateQuery(DateOnly)` | Returns `double` — sums `CaloryLog.Calories` for date (all rows; in Phase 9 all are food intake). |
| `IFoodImageAnalysisService` | Two methods: `AnalyzeImageAsync(byte[], string, CancellationToken)` → multimodal; `RecalculateCaloriesAsync(IReadOnlyList<FoodItemInputDto>, CancellationToken)` → text-only. |
| `FoodImportStateService` | Singleton registered in DI. `Set(items)` called by `ImportFoodActivity`; `HasPending` + `Take()` consumed by `LogViewModel.OnAppearing`. Thread-safe via lock. |
| Import-mode navigation | `ImportFoodActivity` sets `FoodImportStateService` → starts `MainActivity` → `LogViewModel.OnAppearing` detects pending → `Shell.Current.Navigation.PushModalAsync(FoodReviewPage)`. |
| Edit-mode navigation | Calories Today tile tap → `LogViewModel` command → `Shell.Current.Navigation.PushModalAsync(FoodReviewPage)` with today's date. |
| `FoodReviewPage` | Registered as a transient page in `MauiProgram.cs`. Pushed modally (same pattern as LogPage from CalendarViewModel). `FoodReviewViewModel` uses `IMediator` and `FoodImportStateService`. |
| Android Activity | `ImportFoodActivity` extends plain `Activity`. Same fresh-process Gemini key provisioning as Phase 8 (read from SecureStorage key `"gemini_key"`, set on `GeminiKeyHolder` inside `Task.Run`). Loading screen with LeanAI palette. |
| EF Core FK | `FoodLog` → `CaloryLog` configured with `OnDelete(DeleteBehavior.Cascade)` in `OnModelCreating`. |
| `GeminiFoodImageAnalysisService` | `internal sealed class`. `ParseResponse` is `internal static` for testability (same pattern as Phase 8). |
| Date picker | `FoodReviewViewModel` exposes `DateTime SelectedDate`, `DateTime MinDate` (from `GoalStartDate`), `DateTime MaxDate` (today). `OnSelectedDateChanged` partial triggers `LoadFoodForDateAsync` in edit mode only. `UserProfileDto` gained `DateOnly? GoalStartDate`; AutoMapper picks it up by convention. |
| Total calories | `[ObservableProperty] string TotalCaloriesText`. `RefreshTotal()` sums `Items.Sum(r => r.CaloriesValue)`. Called after load, re-evaluate, and via `Items.CollectionChanged` subscription for add/delete. |
| Post-save navigation | After `PopModalAsync`, `FoodReviewViewModel.SaveAsync` sends `FoodSavedMessage(DateOnly)` via `WeakReferenceMessenger`. `LogViewModel` implements `IRecipient<FoodSavedMessage>` and calls `LoadCoreAsync(message.Date)` on the main thread, switching the Daily Log to the saved date. |

---

### Definition of Done
- ✅ Android Share Sheet lists "Import Food" when sharing an image.
- ✅ Sharing a meal photo shows the loading screen then opens LeanAI with a food list populated from Gemini.
- ✅ User can edit food names and quantities, delete individual items, and add new empty rows.
- ✅ "Re-evaluate with AI" updates calorie values for all rows via a Gemini text call.
- ✅ "Save" stores food entries; Calories Today tile shows updated total.
- ✅ Food Review screen header shows an editable DatePicker (min: GoalStartDate; max: today). Changing date in edit mode reloads the food log; in import mode it changes the save target only.
- ✅ Total calories summary row visible below the food list; updates on every add, delete, and re-evaluate.
- ✅ After Save, Daily Log navigates to the date chosen in the picker.
- ✅ Tapping "Calories Today" tile opens the food list in edit mode for today.
- ✅ "Delete All" (with confirmation) removes all food entries; tile resets to "—".
- ✅ `FoodLog` ↔ `CaloryLog` FK with cascade delete verified end-to-end.
- ✅ EF Core migration `Add_FoodTracking` applies cleanly — no existing tables altered.
- ✅ All new command/query handlers tested at 100% branch coverage.
- ✅ `GeminiFoodImageAnalysisService` parse logic tested (valid, markdown-fenced, empty array, missing fields, malformed JSON).
- ✅ `dotnet build` → 0 errors, 0 warnings. `dotnet test` → 157 / 157 tests green.

---

## Post-Phase Changes ✅ COMPLETE

### Share Sheet Entry Point Label Rename
- Removed the `"LeanAI: "` prefix from both Android Share Sheet labels.
  - `"LeanAI: Import Run"` → `"Import Run"`
  - `"LeanAI: Import Food"` → `"Import Food"`
- Changed in: `[Activity(Label = ...)]` and `[IntentFilter(..., Label = ...)]` attributes on `ImportRunActivity` and `ImportFoodActivity`.

---

---

## Phase 10: Import Runs Improvements ✅ COMPLETE

### Goal

Enhance the run-import flow with a review screen (mirroring the food-review screen), activity calorie tracking, and a new Calories Detail screen accessible from the Daily Log tile.

### Functional Requirements

- **Gemini enhancement:** Run image analysis also extracts `calories_burned` (optional 4th metric). Required parameters remain `distance`, `pace`, `duration`; `calories_burned` is returned if shown on the screenshot.
- **Run Review Screen:** After sharing a run screenshot, a review screen appears instead of a direct save. The screen has:
  - A date picker at the top (min = `GoalStartDate`, max = today).
  - A list with two columns: col 1 = activity text (editable), col 2 = calories burned (read-only, AI-only).
  - The original run row has its delete button hidden; custom manually-added rows can be deleted.
  - **Re-evaluate** button — sends all col-1 descriptions to Gemini and updates the calories column.
  - **Total Burned** row at the bottom.
  - Save, Delete All, Cancel buttons.
- **Save behavior:** On Save:
  - For the original run row: appends commentary to `DailyActualWeight.Notes` and saves run metrics (`distance`, `pace`, `duration`, `calories_burned`) to `ActivityLog`.
  - For all rows: saves a `CaloryLog` entry (`SourceType = "activity"`, `Calories`, `Description`).
  - For custom (manually-added) rows: also saves a `CustomActivityLog` entry linked by FK to `CaloryLog`.
- **Custom activity log:** New `CustomActivityLog` table: `Date`, `Description`, `CaloryLogId` FK → `CaloryLogs` (CASCADE delete).
- **Net calories:** The Calories tile on the Daily Log screen shows net calories = food − activity.
- **Calories Detail Screen:** Tapping the Calories tile navigates to a new detail screen showing:
  1. Food items list + Food Total.
  2. Activity items list + Activity Burned total (calories shown as `−N kcal`).
  3. Net Calories grand total.
  - "Edit" buttons in both sections navigate to Food Review / Run Review in edit mode.
- **Edit mode for Run Review:** Loading from DB (`CaloryLog` with `SourceType = "activity"` for the date). On Save in edit mode: delete-then-insert all activity `CaloryLog` rows for the date (no `Notes` append, no `ActivityLog` save).

### New Application Commands / Queries

| Command / Query | Description |
|---|---|
| `AnalyzeRunImageCommand(byte[], string)` | Calls Gemini image analysis, returns `RunImportResultDto` — no persistence. |
| `EstimateActivityCaloriesCommand(IReadOnlyList<string>)` | Sends text descriptions to Gemini; returns `IReadOnlyList<double>` calorie estimates. |
| `SaveRunActivitiesCommand(DateOnly, IReadOnlyList<RunActivityRowDto>, bool IsImportMode)` | Delete-then-insert activity `CaloryLog`; conditionally saves `ActivityLog` + Notes comment (import mode, run rows only); saves `CustomActivityLog` for custom rows. |
| `GetActivityCaloriesForDateQuery(DateOnly)` | Returns `IReadOnlyList<ActivityCaloryLogDto>` for the Calories Detail screen and Run Review edit mode. |

### New Infrastructure Services

| Service | Description |
|---|---|
| `GeminiActivityCaloriesEstimationService` | Implements `IActivityCaloriesEstimationService`; text-only Gemini call returning JSON array of calorie estimates. |
| `RunImportStateService` | Singleton (thread-safe). Stores pending `IReadOnlyList<RunActivityRowDto>` between `ImportRunActivity` and `RunReviewPage`. |

### New Domain Entities / Updates

| Change | Description |
|---|---|
| `CustomActivityLog` | New entity: `Date`, `Description`, `CaloryLogId` FK with `OnDelete(Cascade)`. |
| `CaloryLog.Description` | New nullable string column. |
| `ICaloryLogRepository` | New methods: `AddActivityAsync`, `DeleteActivityCaloriesForDateAsync`, `GetActivityCaloriesForDateAsync`. Updated `GetTotalCaloriesAsync` returns net (food − activity). |

### EF Core Migration

`Add_CustomActivityLogAndCaloryLogDescription` — adds `Description` column to `CaloryLogs`, new `CustomActivityLogs` table.

### Corrections & Clarifications (implemented during Phase 10 bugfix cycle)

- **Import mode is additive for both food and runs.** `SaveFoodLogCommand(IsImportMode: true)` skips `DeleteByDateAsync` — each import appends new food entries alongside existing ones for the date. `SaveRunActivitiesCommand(IsImportMode: false)` performs the delete-then-insert; import mode adds new activity `CaloryLog` rows without removing previously saved entries (manual or prior imports).
- **All activity descriptions are appended to daily notes**, not just the imported run row. After saving, every row's `ActivityText` is joined with newlines and dispatched as one `AppendActivityCommentCommand` block regardless of whether the row is a run or a custom activity.
- **Net calories are computed on-the-fly, not stored.** `GetTotalCaloriesAsync` always queries live data: food calories are summed by joining through `FoodLogs` (so orphaned `CaloryLog` rows from partial deletes are never included), and activity calories are summed from `CaloryLog` where `SourceType = "activity"`. The result (food − activity) can be negative.
- **`FoodLogRepository.DeleteByDateAsync` removes both `FoodLog` and linked `CaloryLog` rows** in the same `SaveChanges` call, preventing orphaned `CaloryLog` entries that would inflate subsequent calorie totals.
- **Calories tile activates when net ≠ 0** (not only when net > 0), so the tile is visible on activity-only days where net calories are negative.
- **`AppendActivityCommentCommand` entries (WeightKg = 0)** are excluded from `GetLogContextQueryHandler` weekly averages and `TodayWeightKg` — only rows where `WeightKg > 0` count as recorded weight entries.
- **`RunImportStateService` and `FoodImportStateService` use static `_lock`/`_pending` fields** so that pending import state survives Android process cold-starts where the DI container is recreated before `MainActivity` opens.
- **`App.NavigateToLogIfNoEntryTodayAsync` is guarded** — navigation to `//Log` is skipped when a modal page (RunReviewPage or FoodReviewPage) is already open, preventing the `InvalidOperationException: Modal Stack is Empty` crash on cold-start share-sheet imports.

### Definition of Done

- ✅ Gemini run analysis returns `calories_burned` (optional).
- ✅ `ImportRunActivity` stores import state and launches `MainActivity` — no toast, no direct save.
- ✅ Run Review screen appears after share-sheet import.
- ✅ Re-evaluate sends text descriptions to Gemini and updates calories column.
- ✅ Manual activity rows can be added, edited, deleted.
- ✅ Save persists to `CaloryLog` (activity) + `CustomActivityLog` (custom rows) + `ActivityLog` + `Notes` (all activity rows, run and custom).
- ✅ Import mode adds entries without removing previously saved food or activity data.
- ✅ Daily Log tile shows net calories (food − activity), computed on-the-fly via live DB query.
- ✅ Calories tile is visible even when net calories are negative (activity-only day).
- ✅ Calories tile navigates to Calories Detail screen.
- ✅ Calories Detail shows food section, activity section, and net grand total; food section total matches the tile.
- ✅ Edit mode for both sections accessible from Calories Detail.
- ✅ `dotnet build` → 0 errors, pre-existing warnings only (SkiaSharp XA0141, MVVMTK0034).
- ✅ 178 / 178 tests passing.

---

## Open Issues

### Launcher Icon — Monochrome Themed Icon ⚠️ UNRESOLVED

**Issue:** On Android 13+ (e.g., Pixel 10) with "Themed icons" enabled, the LeanAI launcher icon renders as a white rounded square instead of a tinted silhouette of the weight-scale icon.

**Root cause:** Without a `<monochrome>` element in the adaptive icon XML, Android falls back to a white disc/square.

**Attempted fix (2026-05-17–18):**
- Added `Platforms/Android/Resources/mipmap-anydpi-v33/leanai_icon.xml` — adaptive icon XML for Android 13+ with `<background>`, `<foreground>`, and `<monochrome>` elements.
- Added `Platforms/Android/Resources/drawable/leanai_icon_monochrome.xml` — Android `VectorDrawable` silhouette of the scale (outer rounded-rect frame via `fillType="evenOdd"` + circle for the display).
- Added `ic_launcher_background` (`#222222`) to `colors.xml`.
- Deleted `Resources/AppIcon/leanai_icon_foreground.svg` (had been inadvertently auto-detected by MAUI's resizetizer, overriding the copper foreground PNG with a white silhouette).

**Current status:** Icon still renders as white rounded square in both colour and monochrome modes. Root cause not yet fully identified. **Deferred — to be revisited.**