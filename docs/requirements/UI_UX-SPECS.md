# UI/UX Specification - LeanAI

## 1. Design Language: 'Industrial Copper'
- **Vibe:** Premium, Dynamic, Motivating, Engineering Precision.
- **Background Model:** Dark Mode only.

## 2. The Color Palette (Luminance & Temperature Logic)
The traditional 'Traffic Light' (Green/Red) system is replaced by the 'Power Level' system based on warmth and glow.

| Hex Code | Use Case | Visual Logic |
| :--- | :--- | :--- |
| **#222222** | **Deep Charcoal Base** | Dark, cold base for extreme contrast. Grounding environment. |
| **#F0F2F5** | **Cold Off-White** | Text, Icons, Neutrals. Luminous off-white with cold temperature. |
| **#D28B5C** | **Luminous Copper** | **SUCCESS / ON-TRACK.** (Replaces Green). Achievement, Energy. |
| **#9A9EAB** | **Matte Nickel** | **WARNING / OFF-TRACK.** (Replaces Yellow/Red). Dimmed, inactive hue. |

## 3. Dynamic UI Behavior (Micro-Animations)
To fulfill the 'Motivating' requirement, the interface responds dynamically to user status.

| App State | Description | Design Interaction |
| :--- | :--- | :--- |
| **Active & On-Track** | Goal Met / Successful Path. | **Luminous Copper (#D28B5C) with "Breathe Pulse".** A slow, bio-luminescent pulse of brightness. |
| **Static & Off-Track** | Off Path / Faded Energy. | **Matte Nickel (#9A9EAB), Flat & Static.** No glow, no pulse. |

## 4. Global Interaction Patterns
- **Autosave Policy:** There is no "Save" button in LeanAI. 
    - **Logic:** Data is persisted via EF Core on property change.
    - **Implementation:** The UI Engineer must use a 500ms debounce on text/numeric inputs to prevent excessive database writes while the user is typing.
- **Units Strategy:** All math in Domain is Metric. The UI Layer performs dynamic conversion for display based on the `UserPreference` setting.

## 5. Navigation & Layout
- **Root Navigation:** .NET MAUI Shell with a Bottom Tab Bar.
- **Tabs:** 
    1. **Log:** Daily Data Entry (or "Evolution" if entry exists).
    2. **Trends:** Graphical Evolution View & Weekly Loss View.
    3. **Settings:** User Stats, Goals, and Units.
- **Inter-view Navigation:** `EvolutionView` uses a Top Segmented Control (Calendar, Line Graph, Bar Graph).

## 6. Screen Logic
- **Setup Wizard:** 
    - Triggered automatically if Profile is incomplete.
    - Question 1: Units (Metric vs Imperial).
    - Question 2: User Stats (Gender, Age, Height, Start Weight).
    - Question 3: Goal (Target Weight, Period).
- **Daily Entry (The "Autosave" Screen):**
    - No "Save" button. Data persists to SQLite via EF Core on property change.
    - **Visual Comparisons (Day 1 Active):**
        - Comparison A: Current Weight vs. Yesterday's Weight (Delta indicator).
        - Comparison B: Current Weight vs. Ideal Weight for [Today] (Warning color if Current > Ideal).
        - Comparison C: Weekly Average vs. Weekly Ideal (Trend prediction).

## 7. Evolution & Calendar Engine
- **Achievement Matrix (Calendar Colors):**
    - **Green:** Weight <= Ideal AND WeeklyAvg <= WeeklyIdeal.
    - **Yellow:** Weight > Ideal BUT WeeklyAvg <= WeeklyIdeal.
    - **Orange:** Weight <= Ideal BUT WeeklyAvg > WeeklyIdeal.
    - **Red:** Weight > Ideal AND WeeklyAvg > WeeklyIdeal.
- **Warning Colors:** Use distinct high-contrast colors (e.g., Red/Orange) specifically when the user is above the Ideal line to drive urgency.
- **Interactivity:** Tapping a Calendar Day navigates to a **Read-Only Summary** of that day. Tapping the "Edit" icon on the summary opens the entry for modification.

## 8. State Management
- **Goal Reset Logic:** If a user changes their Target Weight or Period:
    1. **Purge:** All rows in the `IdealWeight` table are deleted.
    2. **Regenerate:** New `IdealWeight` rows are generated based on the new parameters.
    3. **Preserve:** All `ActualWeight` entries provided by the user MUST be preserved.

## 9. Screen-Specific Rules
- **Setup Wizard:** Stepper-style interface. Cannot be exited until the final "Goal Validation" is saved.
- **Daily Entry:** 
    - Large numeric keypad for weight.
    - Instant visual "Delta" calculation as the user types.

## 10. Interaction Patterns
- **Navigation:** Bottom Tab Bar for Log | Trends | Settings. Segmented Top Control for Evolution Views.
- **Autosave:** 500ms debounce on all numeric/text entries. No "Save" button. PERSIST on property change.