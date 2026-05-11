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

## Phase 4: Daily Tracking & Feedback (The Habit)
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

## Phase 5: The Evolution View (The Dashboard)
- **Goal:** High-level visualization of achievements and trends.
- **Functional Requirements:**
    - **Tabbed Navigation:** Toggle between Calendar and Graphical views.
    - **Calendar Achievement Matrix:** Color-code days (Green, Yellow, Orange, Red) based on Daily/Weekly vs. Ideal thresholds.
    - **Graphical Evolution:** Line chart overlaying `Actual` vs. `Ideal` for the full period.
    - **Weekly Loss View:** Bar chart showing weekly weight delta trends.
- **Technical Specs:** Integration of `Microcharts.Maui`.
- **DoD:** User can navigate their history and visually identify successful vs. struggling periods.