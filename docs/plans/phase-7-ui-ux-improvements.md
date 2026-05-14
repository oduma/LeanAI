# Phase 7: UI/UX Improvements — Implementation Plan

**Status:** Done ✅  
**Branch:** main

---

## 1. Overview

Phase 7 is a pure UI/UX polish pass across all four main screens, plus an auto-save rework for Settings and an Android launcher icon update. No new domain entities, DB tables, or migrations are introduced.

---

## 2. Scope

| Screen | Change |
|--------|--------|
| Log | Larger fonts; more breathing room; renamed Weekly Loss card + date; new Current Average Weekly Weight card; remove debounce hint |
| Trends | Single tap on evolution chart toggles a 3-month zoom; double-tap (second tap) restores full view |
| Calendar | Much larger tap targets on the Previous / Next month arrows |
| Settings | New section order; all fields auto-save (debounced for text, immediate for Picker); Save button removed |
| Android | Replace launcher icon from toolkit at `z-com-ai/icontoolkit/android` |

---

## 3. Detailed Requirements

### 3.1 Log Screen

**Header**
- "Daily Entry" span: `FontSize` 16 → **24**, `FontAttributes="Bold"` (already bold, keep).
- Date span (`EntryDateLabel`): `FontSize` 16 → **20**, `TextColor=ColorCopper` to make it pop.

**Card labels ("Current Weight", "Comments (Optional)")**
- `FontSize` 13 → **15**.

**Spacing between data entry and analysis**
- Increase the gap between the Comments card and the "REAL-TIME ANALYSIS" label from the outer `Spacing="24"` to an explicit `Margin="0,16,0,0"` on the analysis section label — or use a `VerticalStackLayout` with `Spacing="32"` for the outer container.

**Analysis section header**
- "REAL-TIME ANALYSIS" label: `FontSize` 11 → **13**.

**Analysis cards (Yesterday Delta + Weekly Loss)**
- Card inner labels (sub-titles like "Yesterday Delta"): `FontSize` 11 → **13**.
- Value labels (`YesterdayDeltaText`): `FontSize` 22 → **26**.
- Sub-row labels ("Current", "Ideal") inside Weekly Loss: `FontSize` 11 → **13**.
- Sub-row values: `FontSize` 15 → **18**.

**Rename "Predicted Weekly Loss" card**
- Title changes to two lines:
  ```
  Weekly Loss for week starting on:
  [WeekStartDateLabel]
  ```
- `WeekStartDateLabel` is a new `string` ViewModel property formatted as "d MMM yyyy" (e.g., "12 May 2025").

**New "Current Average Weekly Weight" card**
- Positioned after the 2-column grid (same row, new separate card spanning full width).
- Title: "Current Average Weekly Weight" (ColorNickel, FontSize 13).
- Value: formatted weight + unit (e.g., "87.3 kg"), `FontSize=26`, `FontAttributes=Bold`.
- Color: **ColorCopper** if this week's running average < last week's average (good trend); **ColorNickel** otherwise.
- Binding: `CurrentWeeklyAverageText` (string) + `IsWeeklyAverageTrending` (bool → color via `BoolToColorConverter`).

**Remove debounce hint**
- Delete the `Label` with `Text="Data is debounced and saved automatically"`.

### 3.2 Trends Screen — Evolution Chart Zoom

**Behavior**
- First tap on the evolution chart area → **zoom mode**: display only the window from `(today − 76 days)` to `(today + 14 days)` — approximately 2 months + 2 weeks before today and 2 weeks after.
- Second tap → **full mode**: restore the original `FirstDate`→`LastDate` range.
- The Weekly average loss bar chart (Row 2) is **not** affected by the zoom.

**Y-axis in zoom mode**
- Recalculate `yMin`/`yMax` using only data points within the zoom window for better scale granularity.

**Visual indicator of zoom state**
- The evolution chart title changes:
  - Full mode: "Actual weight vs. Ideal Weight"
  - Zoom mode: "Actual weight vs. Ideal Weight  (3-month view)"

### 3.3 Calendar Screen — Arrow Tap Targets

- Replace each `ImageButton` with a full-height transparent `Grid` cell containing the arrow glyph as a `Label`, with `Padding="20,16"` giving a large hit area.
- Column widths increase from `44` → `72` to accommodate larger touch zones.

### 3.4 Settings Screen — Reorganization & Auto-save

**New section order**
1. **GENERIC SETTINGS** — First Day of Week (Picker)
2. **AI CONFIGURATION** — AI Model (text entry) + Gemini API Key (password entry)
3. **DATA IMPORT** — Import from Google Sheets row + Disconnect Google row (conditional)
4. **PROFILE** — Re-Run the Setup row

**Auto-save**
- **Picker (`CalendarFirstDayIndex`)**: Save immediately in `OnCalendarFirstDayIndexChanged`.
- **Text entries (`GeminiModelName`, `GeminiApiKey`)**: Debounced 500 ms (same pattern as `LogViewModel`).
- **Save button**: Removed from the XAML entirely.

**SettingsViewModel changes**
- Remove: `HasChanges`, `SaveAiSettingsCommand`, all `[NotifyCanExecuteChangedFor]`, `RefreshHasChanges()`.
- Add: `CancellationTokenSource? _saveCts`, `SaveWithDebounceAsync()`, `SaveImmediateAsync()`.
- `OnCalendarFirstDayIndexChanged` → calls `SaveImmediateAsync()`.
- `OnGeminiModelNameChanged` / `OnGeminiApiKeyChanged` → calls `TriggerSave()` → `SaveWithDebounceAsync()`.

### 3.5 Android Launcher Icon

- Copy all `mipmap-*` folders from `z-com-ai/icontoolkit/android/res/` into `src/LeanAI.Maui/Platforms/Android/Resources/`.
- Copy `mipmap-anydpi-v26/ic_launcher.xml` as well (adaptive icon descriptor).
- Verify the MAUI `.csproj` does not have a conflicting `<MauiIcon>` that would override the raw resources; if it does, update it to point to the toolkit image or remove it in favor of the raw mipmap approach.

---

## 4. Application Layer Changes

### 4.1 `LogContextDto` — add 3 new fields

```csharp
public sealed record LogContextDto(
    double?    TodayWeightKg,
    string?    TodayNotes,
    double?    YesterdayWeightKg,
    double?    TodayIdealWeightKg,
    double?    WeekFirstWeightKg,
    int        WeekDaysLogged,
    double?    IdealWeeklyLossKg,
    UnitSystem UnitSystem,
    // NEW:
    DateOnly   WeekStartDate,
    double?    CurrentWeekAverageWeightKg,
    double?    LastWeekAverageWeightKg
);
```

### 4.2 `GetLogContextQueryHandler` — compute new fields

- `WeekStartDate` = already-computed `weekStart` (currently local, just return it).
- `CurrentWeekAverageWeightKg` = `weekEntries.Count > 0 ? weekEntries.Average(e => e.WeightKg) : null`.
- `LastWeekAverageWeightKg` = requires one additional `GetRangeAsync(lastWeekStart, lastWeekEnd)` call. `lastWeekStart = weekStart.AddDays(-7)`, `lastWeekEnd = weekStart.AddDays(-1)`.

No new repository methods needed.

---

## 5. ViewModel Changes

### 5.1 `LogViewModel`

**New private fields**
```csharp
private double? _currentWeekAverageKg;
private double? _lastWeekAverageKg;
private DateOnly _weekStartDate;
```

**New observable properties**
```csharp
[ObservableProperty] private string _weekStartDateLabel      = string.Empty;
[ObservableProperty] private string _currentWeeklyAverageText = "—";
[ObservableProperty] private bool   _isWeeklyAverageTrending; // true → ColorCopper
```

**`LoadCoreAsync` additions**
- Store `ctx.WeekStartDate`, `ctx.CurrentWeekAverageWeightKg`, `ctx.LastWeekAverageWeightKg`.
- Set `WeekStartDateLabel = ctx.WeekStartDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)`.

**`RecalculateIndicators` additions**
- After computing `CurrentWeeklyLossText`, compute the weekly average display:
  ```csharp
  if (_currentWeekAverageKg.HasValue)
  {
      CurrentWeeklyAverageText = $"{FormatValue(ToDisplay(_currentWeekAverageKg.Value))} {UnitLabel}";
      IsWeeklyAverageTrending  = _lastWeekAverageKg.HasValue
          && _currentWeekAverageKg.Value < _lastWeekAverageKg.Value;
  }
  else
  {
      CurrentWeeklyAverageText = "—";
      IsWeeklyAverageTrending  = false;
  }
  ```

### 5.2 `TrendsViewModel`

**New state**
```csharp
private bool _isZoomed;
```

**New method (called from code-behind)**
```csharp
public void ToggleZoom()
{
    _isZoomed = !_isZoomed;
    if (_isZoomed)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        EvolutionDrawable.ZoomFrom  = today.AddDays(-76);
        EvolutionDrawable.ZoomTo    = today.AddDays(14);
        EvolutionDrawable.IsZoomed  = true;
    }
    else
    {
        EvolutionDrawable.IsZoomed  = false;
    }
}
```

### 5.3 `SettingsViewModel`

- Remove `HasChanges`, `SaveAiSettingsCommand`, `RefreshHasChanges()`, `[NotifyCanExecuteChangedFor]`.
- Add debounce save pattern (identical to `LogViewModel`):
  ```csharp
  private CancellationTokenSource? _saveCts;

  private void TriggerSave() { _saveCts?.Cancel(); _saveCts = new(); _ = SaveWithDebounceAsync(_saveCts.Token); }

  private async Task SaveWithDebounceAsync(CancellationToken ct)
  {
      try   { await Task.Delay(500, ct); await SaveImmediateAsync(); }
      catch (OperationCanceledException) { }
  }

  private async Task SaveImmediateAsync()
      => await _mediator.Send(new SaveAppSettingsCommand(GeminiModelName, GeminiApiKey, CalendarFirstDay));
  ```
- `OnGeminiModelNameChanged` / `OnGeminiApiKeyChanged` → `TriggerSave()`.
- `OnCalendarFirstDayIndexChanged` → `_ = SaveImmediateAsync()`.

---

## 6. Drawable Changes — `WeightEvolutionDrawable`

**New properties**
```csharp
public bool     IsZoomed { get; set; }
public DateOnly ZoomFrom { get; set; }
public DateOnly ZoomTo   { get; set; }
```

**Internal helpers**
- `EffectiveFirst` = `IsZoomed ? ZoomFrom : FirstDate`
- `EffectiveLast`  = `IsZoomed ? ZoomTo   : LastDate`

**`Draw` updates**
- Pass `EffectiveFirst`/`EffectiveLast` to `DrawXLabels`.
- Recalculate `yMin`/`yMax` from only the points where `point.Date >= EffectiveFirst && point.Date <= EffectiveLast` when zoomed.
- `DateToX` uses `EffectiveFirst`/`EffectiveLast` as the domain (points outside will be off-screen; canvas clips naturally).

**Chart title property**
```csharp
public string ChartTitle =>
    IsZoomed ? "Actual weight vs. Ideal Weight  (3-month view)"
             : "Actual weight vs. Ideal Weight";
```
Bind the `Label` in the XAML to `EvolutionDrawable.ChartTitle` — or expose a `[ObservableProperty] string _evolutionChartTitle` on the ViewModel and update it in `ToggleZoom()`.

For simplicity, expose `EvolutionChartTitle` on `TrendsViewModel` and update it in `ToggleZoom()`.

---

## 7. XAML Changes

### 7.1 `LogPage.xaml`

1. Header `FormattedString`: "Daily Entry" span FontSize 16→24; date span FontSize 16→20 + TextColor=ColorCopper.
2. Outer `VerticalStackLayout`: `Spacing="24"` → `Spacing="28"` (slightly more breathing room overall). Add `Margin="0,24,0,0"` on the "REAL-TIME ANALYSIS" Label.
3. "Current Weight" and "Comments (Optional)" card labels: FontSize 13→15.
4. Analysis header label ("REAL-TIME ANALYSIS"): FontSize 11→13.
5. Card inner sub-title labels (e.g., "Yesterday Delta"): FontSize 11→13.
6. `YesterdayDeltaText` label: FontSize 22→26.
7. Sub-row labels ("Current", "Ideal"): FontSize 11→13; sub-row value labels: FontSize 15→18.
8. Rename card title "Predicted Weekly Loss" to two `Label`s:
   ```xml
   <Label Text="Weekly Loss for week starting on:" ... />
   <Label Text="{Binding WeekStartDateLabel}" ... />
   ```
9. Add new full-width card after the 2-column `Grid`:
   ```xml
   <Border ...>
     <VerticalStackLayout ...>
       <Label Text="Current Average Weekly Weight" FontSize="13" TextColor=ColorNickel />
       <Label Text="{Binding CurrentWeeklyAverageText}"
              TextColor="{Binding IsWeeklyAverageTrending,
                Converter={StaticResource BoolToColorConverter},
                ConverterParameter='ColorCopper|ColorNickel'}"
              FontSize="26" FontAttributes="Bold" />
     </VerticalStackLayout>
   </Border>
   ```
10. Delete the `Label` with `Text="Data is debounced and saved automatically"`.

### 7.2 `TrendsPage.xaml`

- Bind the inner `Label` (chart title, currently hardcoded "Actual weight vs. Ideal Weight") to `{Binding EvolutionChartTitle}`.
- Add `TapGestureRecognizer` with `Tapped="OnEvolutionTapped"` to the `GraphicsView` element:
  ```xml
  <GraphicsView x:Name="EvolutionView" ...>
      <GraphicsView.GestureRecognizers>
          <TapGestureRecognizer Tapped="OnEvolutionTapped" />
      </GraphicsView.GestureRecognizers>
  </GraphicsView>
  ```

### 7.3 `TrendsPage.xaml.cs`

```csharp
private void OnEvolutionTapped(object? sender, TappedEventArgs e)
{
    ViewModel.ToggleZoom();
    EvolutionView.Invalidate();
}
```

### 7.4 `CalendarPage.xaml`

- Change column widths: `ColumnDefinitions="44,*,44"` → `ColumnDefinitions="72,*,72"`.
- Replace each `ImageButton` with a transparent `Grid` containing a `Label` (the glyph) and a `TapGestureRecognizer`:
  ```xml
  <Grid Grid.Column="0" Padding="20,16" BackgroundColor="Transparent">
      <Label Text="&#x276E;" TextColor="{StaticResource ColorCopper}"
             FontSize="20" HorizontalOptions="Center" VerticalOptions="Center" />
      <Grid.GestureRecognizers>
          <TapGestureRecognizer Command="{Binding PreviousMonthCommand}" />
      </Grid.GestureRecognizers>
  </Grid>
  ```
  (Mirror for Next Month with `&#x276F;`.)

### 7.5 `SettingsPage.xaml`

- New section order in XAML: GENERIC SETTINGS → AI CONFIGURATION → DATA IMPORT → PROFILE.
- GENERIC SETTINGS card contains only the "First Day of Week" Picker row.
- AI CONFIGURATION card contains: AI Model entry + Gemini API Key entry (no Save button).
- DATA IMPORT card: unchanged items.
- PROFILE card: Re-Run the Setup row.
- All `Margin` separators between sections remain at `Margin="4,32,0,10"`.
- Save button `Border` block: deleted entirely.

---

## 8. Tests

### Modified test files

| File | Change |
|------|--------|
| `GetLogContextQueryHandlerTests.cs` | Add 3 tests: `WeekStartDate` returned correctly; `CurrentWeekAverage` computed from week entries; `LastWeekAverage` computed from prior-week entries. Update existing week tests to verify new DTO fields. |
| `SaveAppSettingsCommandHandlerTests.cs` | No changes (handler is unchanged). |

### New test count target
Current: **123** → Actual after Phase 7: **127** (4 new tests for `GetLogContextQueryHandler` — `WeekStartDate`, `CurrentWeekAverage`, `LastWeekAverage`, plus one pre-existing test that now covers the new `GetRangeAsync` call pattern).

> SettingsViewModel auto-save logic is not directly unit-tested (it is presentation-layer debounce wiring); behavioral coverage comes from the Application-layer command handler tests which already exist.

---

## 9. Implementation Steps (Ordered)

1. **`LogContextDto`** — add `WeekStartDate`, `CurrentWeekAverageWeightKg`, `LastWeekAverageWeightKg` (Application layer).
2. **`GetLogContextQueryHandler`** — compute and return new fields; add `GetRangeAsync` call for last week.
3. **`GetLogContextQueryHandlerTests`** — add 3 new tests for new fields; update existing tests that construct `LogContextDto` directly (positional record changes will require updates).
4. **`LogViewModel`** — add new private fields, observable properties, and `RecalculateIndicators` logic.
5. **`LogPage.xaml`** — all font, spacing, rename, new card, and debounce label changes.
6. **`WeightEvolutionDrawable`** — add `IsZoomed`, `ZoomFrom`, `ZoomTo`; update `Draw` logic and `DateToX`.
7. **`TrendsViewModel`** — add `EvolutionChartTitle` property; add `ToggleZoom()` method.
8. **`TrendsPage.xaml`** — bind chart title; add `TapGestureRecognizer` on `GraphicsView`.
9. **`TrendsPage.xaml.cs`** — add `OnEvolutionTapped` handler.
10. **`CalendarPage.xaml`** — replace `ImageButton` arrow controls with large-tap `Grid`+`Label` controls.
11. **`SettingsViewModel`** — remove `HasChanges` / `SaveAiSettingsCommand`; add debounce save pattern.
12. **`SettingsPage.xaml`** — reorder sections; remove Save button.
13. **Android icon** — replaced `Resources/AppIcon/leanai_icon.png` with toolkit's `play_store_512.png`; `<MauiIcon>` directive retained so MAUI auto-generates all mipmap densities at build time.
14. **Full test run** — `dotnet test` must be green.
15. **Build** — `dotnet build` → 0 errors.

---

## 10. Definition of Done

- [x] Log screen: header "Daily Entry" + date visibly larger; all analysis fonts enlarged.
- [x] Log screen: "Weekly Loss for week starting on: [Monday date]" replaces "Predicted Weekly Loss"; both Current and Ideal sub-rows retained.
- [x] Log screen: "Current Average Weekly Weight" card present; copper when this week's average < last week's; nickel otherwise.
- [x] Log screen: "Data is debounced and saved automatically" text removed.
- [x] Trends screen: single tap on evolution chart toggles 3-month zoom; second tap restores full view; bar chart unaffected; title reflects state.
- [x] Calendar screen: Previous / Next month arrows have a 72-px-wide, full-padding tap zone.
- [x] Settings screen: sections appear in order GENERIC SETTINGS → AI CONFIGURATION → DATA IMPORT → PROFILE.
- [x] Settings screen: all fields auto-save (Picker immediately; text entries debounced 500 ms); Save button absent.
- [x] Android launcher icon updated with assets from toolkit.
- [x] `dotnet test` → 127 tests, 0 failures.
- [x] `dotnet build` → 0 errors, 0 warnings (pre-existing SkiaSharp XA0141 warning unrelated to Phase 7).
