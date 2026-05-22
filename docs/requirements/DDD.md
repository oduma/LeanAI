# Domain-Driven Design: Bounded Contexts

## Purpose

This document defines the bounded contexts for LeanAI. It exists to prevent concepts from drifting into the wrong context as the app grows. When in doubt about where a new concept belongs, consult the decision guide at the bottom before writing any code.

---

## Contexts at a Glance

```
┌─────────────────────┐   ┌─────────────────────┐
│   WeightManagement  │   │    FoodTracking      │
│  "Who & how much"   │   │  "What was eaten"    │
└─────────────────────┘   └─────────────────────┘
┌─────────────────────┐   ┌─────────────────────┐
│  ActivityTracking   │   │   EnergyTracking     │
│  "What was done"    │   │  "What is the net"   │
└─────────────────────┘   └─────────────────────┘
                           ┌─────────────────────┐
                           │      Routine         │
                           │ "What repeats daily" │
                           └─────────────────────┘
```

---

## WeightManagement

**Purpose:** Everything about the user as a physical person and their weight goal.

**Owns:**
- User profile: demographics (age, gender, height), unit system preference
- Weight goal: target weight, target period, start date, end date
- Ideal weight trajectory (the mathematically generated daily target)
- Daily actual weight entries and their optional notes
- Weekly average weights
- App-level configuration: AI model name, calendar first-day preference, feature toggles (e.g. Use BMR)

**Does NOT own:**
- What the user ate
- What activities the user performed
- Any calorie figure — including BMR output. BMR is *computed* here from profile data, but the resulting energy entry belongs in EnergyTracking.

**Integration:** Provides user profile data (height, age, gender, unit system) and weight entries to the Application layer. Other contexts query it via MediatR when they need profile facts (e.g. BMR calculation reads height/age/gender from here).

---

## FoodTracking

**Purpose:** The identity and description of food items the user consumed.

**Owns:**
- A food log entry: food item name, quantity (free text), the date it was consumed, and its estimated caloric value as a food-specific property
- The full list of food entries for a given date

**Does NOT own:**
- Activity descriptions or calorie burn from exercise
- The central energy ledger (the aggregate that sums all calorie sources for a date)
- Routine definitions — even if the routine item is a food item

**Integration:** When a food log is saved, the Application layer writes the caloric contribution to EnergyTracking. FoodTracking does not reference EnergyTracking directly.

---

## ActivityTracking

**Purpose:** What the user did physically — runs, custom activities, and any metrics captured from them.

**Owns:**
- Run records: metrics extracted from a run (distance, pace, duration)
- Custom activity entries: a user-written description and an estimated calorie burn
- The full list of activity entries for a given date

**Does NOT own:**
- Food descriptions or food calorie intake
- The central energy ledger
- Routine definitions — even if the routine item is an activity

**Integration:** When an activity is saved, the Application layer writes the calorie expenditure to EnergyTracking. ActivityTracking does not reference EnergyTracking directly.

---

## EnergyTracking

**Purpose:** The daily energy ledger — the aggregate view of all caloric contributions from any source.

**Owns:**
- The central calorie ledger entry: source type (food / activity / bmr), signed caloric value, date
- The link back to the originating entry in its source context (e.g. which food log row this entry came from), held as a bare reference (not a foreign key constraint), so that each context can manage its own data lifecycle
- The query for net daily calories: food intake − activity burn − BMR
- The query for total calories by source type

**Does NOT own:**
- The names, quantities, or descriptions of food or activities — those details live in their originating context
- User profile or weight data

**Integration:** This context is written to by the Application layer whenever FoodTracking, ActivityTracking, or WeightManagement (BMR) performs a save. It is read by the Presentation layer (via MediatR query) to display the calorie tile and the calorie detail screen. No other context depends on EnergyTracking.

---

## Routine

**Purpose:** The user's recurring set of food and activity items that can be applied to any day as a shortcut.

**Owns:**
- Routine item definitions: a description, optional quantity, source type (food or activity), and a stored calorie snapshot
- The per-day status of whether the routine has been applied to a given date

**Does NOT own:**
- Food logs or activity logs for any specific date — applying the routine *creates* entries in FoodTracking and ActivityTracking (and through them, in EnergyTracking), but the created entries are owned by those contexts
- Any live calorie calculation — calories in a routine item are a fixed snapshot taken at the time the routine was last saved

**Integration:** Applying the routine is an Application-layer orchestration: the command handler reads from Routine, writes to FoodTracking, writes to ActivityTracking, and (indirectly via those saves) writes to EnergyTracking. The Routine context never references FoodTracking or ActivityTracking entities directly.

---

## Integration Rules

1. **No domain-to-domain references.** A domain entity in one context must never hold a reference to a domain entity in another context. Cross-context links are held as bare IDs (Guids) only.
2. **The Application layer orchestrates cross-context operations.** When a use case touches more than one context (e.g. "apply routine" writes to Routine, FoodTracking, ActivityTracking, and EnergyTracking), that orchestration lives in an Application command handler — not in any domain entity.
3. **Contexts communicate outward through events or inward through queries, never through direct service calls between domain layers.**
4. **EnergyTracking is write-only from other contexts' perspective.** FoodTracking and ActivityTracking never read from EnergyTracking; they only trigger writes to it via the Application layer.

---

## Decision Guide — Where does this new concept belong?

| Question | → Context |
|----------|-----------|
| Does it describe the user's body, goal, or weight entry? | WeightManagement |
| Does it describe *what the user ate* (name, portion)? | FoodTracking |
| Does it describe *what the user did* physically (activity, run metrics)? | ActivityTracking |
| Does it represent a caloric contribution to the daily balance (positive or negative)? | EnergyTracking |
| Does it represent something the user does *repeatedly across multiple days*? | Routine |
| Does it span more than one of the above? | It is an Application-layer orchestration concern, not a new entity. If it recurs, consider whether a new context is warranted. |
| **Does it not fit any of the above?** | **Stop. Do not force it into the nearest context. Ask the user: "This concept doesn't fit any existing bounded context — should we create a new one, or does it extend an existing context's responsibility?"** |

**Red flags — the concept is in the wrong context if:**
- An entity has a string `SourceType` field discriminating between "food" and "activity" → it is crossing context boundaries and belongs in a neutral context (EnergyTracking for calorie-related types; Routine for recurring patterns)
- A context's repository is injected into another context's *domain* layer
- A context owns entities that describe things from two different user-facing concerns
