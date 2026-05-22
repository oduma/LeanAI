# Phase 13: DDD Boundary Refactor — Implementation Plan

## Overview

Pure structural refactor. No user-visible behaviour changes. Every entity, repository, and Application use case is moved to the bounded context that owns it per [DDD.md](../requirements/DDD.md). Two new contexts are created (`EnergyTracking`, `Routine`); `ActivityTracking` is extended; `FoodTracking` is reduced to food-only concerns.

**Clarifications locked in:**
- Q1: `CaloryLog` → `EnergyLog` (rename entity and table)
- Q2: Strict — all cross-context FK constraints removed; cascade handled explicitly in Application layer
- Q3: `RoutineItem` split into `RoutineFoodItem` + `RoutineActivityItem`
- Q4: `CustomActivityLog` → `ActivityTracking`
- Q5: `CalculateAndSaveBmrCommand` / `GetBmrForDateQuery` stay in `WeightManagement`

**Execution strategy:** bottom-up (Domain → Infrastructure → Application → Presentation → Tests). Build must compile after each step. Tests run only at the end.

---

## Step 1 — Domain: Create `EnergyTracking` context

**New files:**

`src/LeanAI.Domain/EnergyTracking/Entities/EnergyLog.cs`
- Namespace: `LeanAI.Domain.EnergyTracking.Entities`
- Identical properties to current `CaloryLog` (`Id`, `Date`, `Calories`, `SourceType`, `Description?`, `RoutineItemId?`) — only namespace and class name change

`src/LeanAI.Domain/EnergyTracking/Interfaces/IEnergyLogRepository.cs`
- Namespace: `LeanAI.Domain.EnergyTracking.Interfaces`
- All methods identical to `ICaloryLogRepository` but typed to `EnergyLog` instead of `CaloryLog`:
  - `AddAsync(EnergyLog log)`
  - `AddRangeAsync(IEnumerable<EnergyLog> logs)`
  - `AddRoutineCopyAsync(EnergyLog log)`
  - `GetRoutineCopiesForDateAsync(DateOnly date)`
  - `DeleteRoutineCopyAsync(EnergyLog log)` — deletes the `EnergyLog` row only (no cascade; Application layer handles linked rows)
  - `DeleteActivityCaloriesForDateAsync(DateOnly date)` — deletes `EnergyLog` rows only
  - `GetActivityCaloriesForDateAsync(DateOnly date)`
  - `GetTotalCaloriesAsync(DateOnly date)`
  - `GetBmrForDateAsync(DateOnly date)`

---

## Step 2 — Domain: Create `Routine` context

**New files:**

`src/LeanAI.Domain/Routine/Entities/RoutineFoodItem.cs`
- Namespace: `LeanAI.Domain.Routine.Entities`
- Properties: `Id` (Guid), `Description` (string), `Quantity` (string?), `Calories` (double)

`src/LeanAI.Domain/Routine/Entities/RoutineActivityItem.cs`
- Namespace: `LeanAI.Domain.Routine.Entities`
- Properties: `Id` (Guid), `Description` (string), `Calories` (double)

`src/LeanAI.Domain/Routine/Entities/DailyRoutineStatus.cs`
- Move from `LeanAI.Domain.FoodTracking.Entities` — namespace update only

`src/LeanAI.Domain/Routine/Interfaces/IRoutineRepository.cs`
- Namespace: `LeanAI.Domain.Routine.Interfaces`
- Methods:
  - `GetAllFoodItemsAsync(CancellationToken ct)` → `IReadOnlyList<RoutineFoodItem>`
  - `GetAllActivityItemsAsync(CancellationToken ct)` → `IReadOnlyList<RoutineActivityItem>`
  - `ReplaceAllAsync(IReadOnlyList<RoutineFoodItem> foodItems, IReadOnlyList<RoutineActivityItem> activityItems, CancellationToken ct)` — deletes all food items and all activity items, then inserts provided lists, in one transaction
  - `GetIsActiveForDateAsync(DateOnly date, CancellationToken ct)` → `bool`
  - `SetIsActiveForDateAsync(DateOnly date, bool isActive, CancellationToken ct)`

---

## Step 3 — Domain: Extend `ActivityTracking` context

**New files:**

`src/LeanAI.Domain/ActivityTracking/Entities/CustomActivityLog.cs`
- Move from `LeanAI.Domain.FoodTracking.Entities`
- Namespace: `LeanAI.Domain.ActivityTracking.Entities`
- Rename property `CaloryLogId` → `EnergyLogId` (bare `Guid` — no navigation property, no FK)

`src/LeanAI.Domain/ActivityTracking/Interfaces/ICustomActivityLogRepository.cs`
- Move from `LeanAI.Domain.FoodTracking.Interfaces`
- Namespace: `LeanAI.Domain.ActivityTracking.Interfaces`
- Add method: `DeleteByEnergyLogIdsAsync(IReadOnlyList<Guid> energyLogIds, CancellationToken ct)` — needed for explicit cascade by Application handlers

---

## Step 4 — Domain: Update `FoodTracking` context

**Updated files:**

`src/LeanAI.Domain/FoodTracking/Entities/FoodLog.cs`
- Rename property `CaloryLogId` → `EnergyLogId`
- Remove navigation property `public CaloryLog CaloryLog { get; set; }` (cross-context FK removed)
- `EnergyLogId` is now a plain `Guid` (not nullable — still required for the food entry to make sense)

**Deleted files:**
- `src/LeanAI.Domain/FoodTracking/Entities/CaloryLog.cs`
- `src/LeanAI.Domain/FoodTracking/Entities/CustomActivityLog.cs`
- `src/LeanAI.Domain/FoodTracking/Entities/RoutineItem.cs`
- `src/LeanAI.Domain/FoodTracking/Entities/DailyRoutineStatus.cs`
- `src/LeanAI.Domain/FoodTracking/Interfaces/ICaloryLogRepository.cs`
- `src/LeanAI.Domain/FoodTracking/Interfaces/ICustomActivityLogRepository.cs`
- `src/LeanAI.Domain/FoodTracking/Interfaces/IRoutineRepository.cs`

---

## Step 5 — Infrastructure: Create `EnergyTracking` repository

`src/LeanAI.Infrastructure/EnergyTracking/Repositories/EnergyLogRepository.cs`
- Namespace: `LeanAI.Infrastructure.EnergyTracking.Repositories`
- Implements `IEnergyLogRepository`
- Logic identical to current `CaloryLogRepository` — only entity type and table references updated
- `DeleteRoutineCopyAsync`: deletes the `EnergyLog` row only; does NOT touch `FoodLog` or `CustomActivityLog` (Application layer responsibility)
- `DeleteActivityCaloriesForDateAsync`: deletes `EnergyLog` rows with `SourceType = "activity"` for date only

---

## Step 6 — Infrastructure: Create `Routine` repository

`src/LeanAI.Infrastructure/Routine/Repositories/RoutineRepository.cs`
- Namespace: `LeanAI.Infrastructure.Routine.Repositories`
- Implements `IRoutineRepository`
- `ReplaceAllAsync`: in one `SaveChangesAsync` call — `RemoveRange` all `RoutineFoodItems`, `RemoveRange` all `RoutineActivityItems`, `AddRange` food items, `AddRange` activity items
- `SetIsActiveForDateAsync`: upsert on `DailyRoutineStatuses` (find by date, create if not found)

---

## Step 7 — Infrastructure: Move `CustomActivityLog` repository

`src/LeanAI.Infrastructure/ActivityTracking/Repositories/CustomActivityLogRepository.cs`
- Move from `LeanAI.Infrastructure.FoodTracking.Repositories`
- Namespace: `LeanAI.Infrastructure.ActivityTracking.Repositories`
- Update all `CaloryLogId` references → `EnergyLogId`
- Implement new `DeleteByEnergyLogIdsAsync` method

**Deleted:**
- `src/LeanAI.Infrastructure/FoodTracking/Repositories/CaloryLogRepository.cs`
- `src/LeanAI.Infrastructure/FoodTracking/Repositories/CustomActivityLogRepository.cs`
- `src/LeanAI.Infrastructure/FoodTracking/Repositories/RoutineRepository.cs`

---

## Step 8 — Infrastructure: Update `LeanAIDbContext` and create migration

### 8.1 DbContext changes (`LeanAIDbContext.cs`)

**DbSets:**
- Remove `DbSet<CaloryLog>` → add `DbSet<EnergyLog> EnergyLogs`
- Remove `DbSet<RoutineItem>` → add `DbSet<RoutineFoodItem> RoutineFoodItems` and `DbSet<RoutineActivityItem> RoutineActivityItems`
- `DbSet<DailyRoutineStatus>` stays but namespace updated
- `DbSet<CustomActivityLog>` stays but namespace updated

**Model configurations (`OnModelCreating`):**

*EnergyLog (was CaloryLog):*
- Table name: `EnergyLogs`
- All property configs identical to current `CaloryLog` config
- No FK to `FoodLogs` or `CustomActivityLogs`

*FoodLog:*
- Remove `HasForeignKey(f => f.CaloryLogId)` / `OnDelete(DeleteBehavior.Cascade)` config entirely
- Map `EnergyLogId` as a plain column (no FK configuration)

*CustomActivityLog:*
- Remove `HasForeignKey(c => c.CaloryLogId)` / `OnDelete(DeleteBehavior.Cascade)` config entirely
- Map `EnergyLogId` as a plain column

*RoutineFoodItem:*
- Table: `RoutineFoodItems`; key `Id`; required: `Description`, `Calories`; optional: `Quantity`

*RoutineActivityItem:*
- Table: `RoutineActivityItems`; key `Id`; required: `Description`, `Calories`

*Remove `RoutineItem` configuration entirely*

### 8.2 Migration: `Refactor_DDD_Boundaries`

Run: `dotnet ef migrations add Refactor_DDD_Boundaries --project src/LeanAI.Infrastructure`

The generated migration must produce the following `Up()` operations (verify and amend if the scaffolder omits any):

```
1. RenameTable("CaloryLogs", "EnergyLogs")

2. RenameColumn("FoodLogs", "CaloryLogId", "EnergyLogId")
   DropForeignKey on FoodLogs.EnergyLogId (if constraint existed)

3. RenameColumn("CustomActivityLogs", "CaloryLogId", "EnergyLogId")
   DropForeignKey on CustomActivityLogs.EnergyLogId

4. CreateTable("RoutineFoodItems"):
   Id TEXT PK, Description TEXT NOT NULL, Quantity TEXT NULL, Calories REAL NOT NULL

5. CreateTable("RoutineActivityItems"):
   Id TEXT PK, Description TEXT NOT NULL, Calories REAL NOT NULL

6. Data migration (raw SQL via migrationBuilder.Sql()):
   INSERT INTO RoutineFoodItems (Id, Description, Quantity, Calories)
     SELECT Id, Description, Quantity, Calories FROM RoutineItems WHERE SourceType = 'food';

   INSERT INTO RoutineActivityItems (Id, Description, Calories)
     SELECT Id, Description, Calories FROM RoutineItems WHERE SourceType = 'activity';

7. DropTable("RoutineItems")
```

`Down()` must reverse all operations (recreate `RoutineItems`, migrate data back, drop new tables, rename columns and table back).

Verify migration does NOT drop `EnergyLogs`, `FoodLogs`, `CustomActivityLogs`, or `DailyRoutineStatuses`.

### 8.3 DI Registration (`DependencyInjection.cs`)

Replace:
- `ICaloryLogRepository → CaloryLogRepository` with `IEnergyLogRepository → EnergyLogRepository`
- `IRoutineRepository → RoutineRepository` — namespace updated
- `ICustomActivityLogRepository → CustomActivityLogRepository` — namespace updated

---

## Step 9 — Application: Create `EnergyTracking` use cases

New folder: `src/LeanAI.Application/EnergyTracking/`

### 9.1 DTOs

`src/LeanAI.Application/EnergyTracking/DTOs/ActivityEnergyLogDto.cs`
- Rename from `ActivityCaloryLogDto`; same fields; namespace `LeanAI.Application.EnergyTracking.DTOs`

### 9.2 Queries

`GetTotalCaloriesForDateQuery` — move from `FoodTracking\Queries\GetTotalCaloriesForDate\`
- New path: `EnergyTracking/Queries/GetTotalCaloriesForDate/`
- Handler: inject `IEnergyLogRepository` (not `ICaloryLogRepository`)

`GetActivityCaloriesForDateQuery` — move from `FoodTracking\Queries\GetActivityCaloriesForDate\`
- New path: `EnergyTracking/Queries/GetActivityCaloriesForDate/`
- Handler: inject `IEnergyLogRepository`; return type updated to `IReadOnlyList<ActivityEnergyLogDto>`

**Deleted from FoodTracking:**
- `FoodTracking/Queries/GetTotalCaloriesForDate/`
- `FoodTracking/Queries/GetActivityCaloriesForDate/`
- `FoodTracking/DTOs/ActivityCaloryLogDto.cs`

---

## Step 10 — Application: Create `Routine` use cases

New folder: `src/LeanAI.Application/Routine/`

### 10.1 DTOs

`src/LeanAI.Application/Routine/DTOs/RoutineFoodItemDto.cs`
- `record RoutineFoodItemDto(Guid Id, string Description, string? Quantity, double Calories)`

`src/LeanAI.Application/Routine/DTOs/RoutineActivityItemDto.cs`
- `record RoutineActivityItemDto(Guid Id, string Description, double Calories)`

`src/LeanAI.Application/Routine/DTOs/RoutineItemsResultDto.cs`
- `record RoutineItemsResultDto(IReadOnlyList<RoutineFoodItemDto> FoodItems, IReadOnlyList<RoutineActivityItemDto> ActivityItems)`

### 10.2 Queries

`GetRoutineItemsQuery` — move from `FoodTracking\Queries\GetRoutineItems\`
- New path: `Routine/Queries/GetRoutineItems/`
- Returns `RoutineItemsResultDto` (two typed lists, one round trip)
- Handler: calls `GetAllFoodItemsAsync` and `GetAllActivityItemsAsync` on `IRoutineRepository`

`GetRoutineStatusForDateQuery` — move from `FoodTracking\Queries\GetRoutineStatusForDate\`
- New path: `Routine/Queries/GetRoutineStatusForDate/`
- Handler: inject `IRoutineRepository`

### 10.3 Commands

`SaveRoutineFromDayCommand` — move from `FoodTracking\Commands\SaveRoutineFromDay\`
- New path: `Routine/Commands/SaveRoutineFromDay/`
- Command signature: `SaveRoutineFromDayCommand(IReadOnlyList<RoutineFoodItemDto> FoodItems, IReadOnlyList<RoutineActivityItemDto> ActivityItems)`
- Handler: maps DTOs to `RoutineFoodItem` / `RoutineActivityItem` entities; calls `IRoutineRepository.ReplaceAllAsync(foodItems, activityItems)`

`ApplyRoutineForDateCommand` — move from `FoodTracking\Commands\ApplyRoutineForDate\`
- New path: `Routine/Commands/ApplyRoutineForDate/`
- Handler injects: `IRoutineRepository`, `IFoodLogRepository`, `IEnergyLogRepository`, `ICustomActivityLogRepository`
- For each `RoutineFoodItem`:
  1. Insert `EnergyLog { SourceType="food", Calories, RoutineItemId=item.Id }` via `IEnergyLogRepository.AddAsync` → get new Id
  2. Insert `FoodLog { Description, Quantity, EnergyLogId=energyLog.Id }` via `IFoodLogRepository`
- For each `RoutineActivityItem`:
  1. Insert `EnergyLog { SourceType="activity", Calories, Description, RoutineItemId=item.Id }` → get new Id
  2. Insert `CustomActivityLog { Description, EnergyLogId=energyLog.Id }` via `ICustomActivityLogRepository`
- Call `IRoutineRepository.SetIsActiveForDateAsync(date, true)`

`RemoveUnmodifiedRoutineItemsForDateCommand` — move from `FoodTracking\Commands\RemoveUnmodifiedRoutineItems\`
- New path: `Routine/Commands/RemoveUnmodifiedRoutineItems/`
- Handler injects: `IRoutineRepository`, `IEnergyLogRepository`, `IFoodLogRepository`, `ICustomActivityLogRepository`
- Load `EnergyLog` entries for date where `RoutineItemId IS NOT NULL`
- Separate into food list (SourceType="food") and activity list (SourceType="activity")
- Load `RoutineFoodItem`s matching food RoutineItemIds via `IRoutineRepository.GetAllFoodItemsAsync()`
- Load `RoutineActivityItem`s matching activity RoutineItemIds via `IRoutineRepository.GetAllActivityItemsAsync()`
- For each food `EnergyLog`:
  - Look up matching `RoutineFoodItem` by `RoutineItemId`
  - If not found → skip (preserve)
  - If `EnergyLog.Calories == item.Calories AND FoodLog.Description == item.Description AND FoodLog.Quantity == item.Quantity` → delete: `IFoodLogRepository.DeleteByIdAsync`, then `IEnergyLogRepository.DeleteAsync`
  - Otherwise → skip (modified, preserve)
- For each activity `EnergyLog`:
  - Look up matching `RoutineActivityItem` by `RoutineItemId`
  - If not found → skip (preserve)
  - If `EnergyLog.Calories == item.Calories AND EnergyLog.Description == item.Description` → delete: `ICustomActivityLogRepository.DeleteByEnergyLogIdAsync`, then `IEnergyLogRepository.DeleteAsync`
  - Otherwise → skip
- Call `IRoutineRepository.SetIsActiveForDateAsync(date, false)`

**Deleted from FoodTracking:**
- `FoodTracking/Queries/GetRoutineItems/`
- `FoodTracking/Queries/GetRoutineStatusForDate/`
- `FoodTracking/Commands/SaveRoutineFromDay/`
- `FoodTracking/Commands/ApplyRoutineForDate/`
- `FoodTracking/Commands/RemoveUnmodifiedRoutineItems/`
- `FoodTracking/DTOs/RoutineItemDto.cs`

---

## Step 11 — Application: Update `FoodTracking` use cases

`SaveFoodLogCommandHandler`:
- Now injects `IEnergyLogRepository` in addition to `IFoodLogRepository`
- Delete path (full replace): 
  1. Load all `FoodLog`s for date → collect their `EnergyLogId`s
  2. Delete `EnergyLog`s by those IDs via `IEnergyLogRepository.DeleteManyAsync(ids)`
  3. Delete `FoodLog`s via `IFoodLogRepository.DeleteByDateAsync(date)`
- Insert path: create `EnergyLog` first (get Id), then create `FoodLog { EnergyLogId = energyLog.Id }`
- Add `DeleteManyAsync(IReadOnlyList<Guid> ids)` to `IEnergyLogRepository` and `EnergyLogRepository` if not already present

`DeleteFoodLogForDateCommandHandler`:
- Same explicit two-step delete as above

`GetFoodLogForDateQueryHandler`:
- No longer eager-loads via FK navigation; instead loads `FoodLog`s for date, then loads matching `EnergyLog`s by their `EnergyLogId`s; joins in memory to build `FoodLogEntryDto`

`FoodLogEntryDto`:
- Rename field `CaloryLogId` → `EnergyLogId` if exposed

---

## Step 12 — Application: Update `ActivityTracking` use cases

`SaveRunActivitiesCommandHandler`:
- Replace `ICaloryLogRepository` injection with `IEnergyLogRepository`
- Replace all `CaloryLog` entity references with `EnergyLog`
- Delete path (edit mode):
  1. Load `EnergyLog`s for date where `SourceType = "activity"` → collect IDs
  2. Call `ICustomActivityLogRepository.DeleteByEnergyLogIdsAsync(ids)` (explicit delete)
  3. Call `IEnergyLogRepository.DeleteActivityCaloriesForDateAsync(date)`
- `CustomActivityLog.CaloryLogId` → `CustomActivityLog.EnergyLogId` in all entity construction

---

## Step 13 — Application: Update `WeightManagement` use cases

`CalculateAndSaveBmrCommandHandler`:
- Replace `ICaloryLogRepository` injection with `IEnergyLogRepository`
- Replace `CaloryLog` construction with `EnergyLog`

`GetBmrForDateQueryHandler`:
- Replace `ICaloryLogRepository` injection with `IEnergyLogRepository`

---

## Step 14 — Presentation: Update MAUI ViewModels

`CaloriesDetailViewModel`:
- Update `using` statements: `ActivityCaloryLogDto` → `ActivityEnergyLogDto` (from `EnergyTracking.DTOs`)
- `SaveRoutineAsync`: build two separate lists — `IReadOnlyList<RoutineFoodItemDto>` from checked food VMs, `IReadOnlyList<RoutineActivityItemDto>` from checked activity VMs; send `SaveRoutineFromDayCommand(foodItems, activityItems)`
- Update `using` for Routine commands to new `Routine.Commands.*` namespace

`CaloriesDetailActivityItemViewModel`:
- Update `Dto` property type from `ActivityCaloryLogDto` to `ActivityEnergyLogDto`

`RoutineManagementViewModel`:
- `InitialiseAsync`: `GetRoutineItemsQuery` now returns `RoutineItemsResultDto`; populate `FoodItems` from `result.FoodItems`, `ActivityItems` from `result.ActivityItems`
- `SaveCommand`: send `SaveRoutineFromDayCommand` with two typed lists (food and activity)
- Update DTO construction to use `RoutineFoodItemDto` and `RoutineActivityItemDto`

All other ViewModels / Views referencing old namespaces: update `using` statements only.

---

## Step 15 — Tests: Reorganise and update

### 15.1 Move test files

| Current location | New location |
|------------------|-------------|
| `FoodTracking/Queries/GetTotalCaloriesForDateQueryHandlerTests.cs` | `EnergyTracking/Queries/` |
| `FoodTracking/Queries/GetActivityCaloriesForDateQueryHandlerTests.cs` | `EnergyTracking/Queries/` |
| `FoodTracking/Queries/GetRoutineItemsQueryHandlerTests.cs` | `Routine/Queries/` |
| `FoodTracking/Queries/GetRoutineStatusForDateQueryHandlerTests.cs` | `Routine/Queries/` |
| `FoodTracking/Commands/ApplyRoutineForDateCommandHandlerTests.cs` | `Routine/Commands/` |
| `FoodTracking/Commands/SaveRoutineFromDayCommandHandlerTests.cs` | `Routine/Commands/` |
| `FoodTracking/Commands/RemoveUnmodifiedRoutineItemsForDateCommandHandlerTests.cs` | `Routine/Commands/` |

Stays in `FoodTracking/`:
- `AnalyzeFoodImageCommandHandlerTests.cs`
- `SaveFoodLogCommandHandlerTests.cs`
- `DeleteFoodLogForDateCommandHandlerTests.cs`
- `RecalculateCaloriesCommandHandlerTests.cs`
- `GetFoodLogForDateQueryHandlerTests.cs`

### 15.2 Update all moved test files

- Update `using` statements to new namespaces
- Replace `Mock<ICaloryLogRepository>` with `Mock<IEnergyLogRepository>`
- Replace `ActivityCaloryLogDto` with `ActivityEnergyLogDto`
- Replace `RoutineItemDto` with `RoutineFoodItemDto` / `RoutineActivityItemDto`
- Replace `GetRoutineItemsQuery` return type assertions: now verifies `RoutineItemsResultDto.FoodItems` and `.ActivityItems`
- Replace `SaveRoutineFromDayCommand` constructor call: now takes two lists
- Replace `ApplyRoutineForDateCommand` handler test mocks: `IEnergyLogRepository` instead of `ICaloryLogRepository`

### 15.3 Update `SaveFoodLogCommandHandlerTests.cs`

- Verify `IEnergyLogRepository.DeleteManyAsync` is called with correct IDs (explicit cascade, no FK)
- Verify `EnergyLog` is inserted before `FoodLog`; `FoodLog.EnergyLogId` is set to the returned `EnergyLog.Id`

### 15.4 Update `SaveRunActivitiesCommandHandlerTests.cs`

- Verify `ICustomActivityLogRepository.DeleteByEnergyLogIdsAsync` is called in edit mode before `IEnergyLogRepository.DeleteActivityCaloriesForDateAsync`
- Verify `CustomActivityLog.EnergyLogId` is set correctly

### 15.5 Update `WeightManagement` tests

- `CalculateAndSaveBmrCommandHandlerTests.cs`: replace `Mock<ICaloryLogRepository>` with `Mock<IEnergyLogRepository>`
- `GetBmrForDateQueryHandlerTests.cs`: same

---

## Step 16 — Final verification

1. `dotnet build` → 0 errors, 0 new warnings
2. `dotnet test` → all tests green (190+ expected; no test count regression)
3. Manually verify EF Core migration snapshot is consistent with the updated model

---

## Definition of Done

- [x] `EnergyLog` and `IEnergyLogRepository` in `LeanAI.Domain/EnergyTracking/`
- [x] `RoutineFoodItem`, `RoutineActivityItem`, `DailyRoutineStatus`, `IRoutineRepository` in `LeanAI.Domain/Routine/`
- [x] `CustomActivityLog`, `ICustomActivityLogRepository` in `LeanAI.Domain/ActivityTracking/`
- [x] `FoodTracking` domain contains only `FoodLog` and `IFoodLogRepository`
- [x] No EF Core FK constraints cross context boundaries (`FoodLog.EnergyLogId` and `CustomActivityLog.EnergyLogId` are plain columns)
- [x] Cascade handled explicitly by Application-layer command handlers
- [x] EF Core migration `Refactor_DDD_Boundaries` applies cleanly — zero data loss
- [x] All Application commands/queries in correct context namespaces
- [x] All test files relocated; all mocks and DTO references updated
- [x] `dotnet build` → 0 errors. `dotnet test` → 209 / 209 tests green.

## Corrections (post-implementation)

- **Missing Designer file.** `20260522000000_Refactor_DDD_Boundaries.Designer.cs` was omitted. EF Core silently skipped the migration at runtime (no `[Migration(...)]` attribute to discover), leaving `CaloryLogs` in place and crashing on first query. Fixed by creating the Designer file with the correct attribute and `BuildTargetModel` matching the snapshot.

## Status: ✅ COMPLETE (2026-05-22)
