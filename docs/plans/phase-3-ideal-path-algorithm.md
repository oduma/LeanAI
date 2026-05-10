# Phase 3 Plan: The Ideal Path (The Algorithm)

> **Status: COMPLETE** — Delivered 2026-05-10

---

## 1. Goal

After wizard completion (first-run and re-run), calculate the user's linear daily ideal weight trajectory and persist one row per calendar day to SQLite — without blocking the UI.

---

## 2. Algorithm

```
DailyLoss = (StartingWeightKg - TargetWeightKg) / TotalDays

For day index i in [0 … TotalDays − 1]:
    Date[i]      = StartDate + i days
    WeightKg[i]  = StartingWeightKg − (DailyLoss × i)
```

Key decisions (confirmed with user):

| Decision | Choice |
|---|---|
| StartDate | `DateTime.Today` at wizard completion |
| TotalDays | `TargetPeriodExtensions.TotalDays()` — 90 / 180 / 365 |
| Day 0 value | `StartingWeightKg` (user's current weight) |
| Day TotalDays−1 value | `≈ TargetWeightKg` (within one `DailyLoss`) |
| Goal Reset triggers | First-run wizard, Re-run wizard, Future settings screen |
| ActualWeight table | Created as empty placeholder now (Phase 4 fills it) |

---

## 3. Domain Layer — New Files

### 3.1 `DailyIdealWeight` Entity

**Path:** `src/LeanAI.Domain/WeightManagement/Entities/DailyIdealWeight.cs`

```csharp
public class DailyIdealWeight : BaseEntity
{
    public DateOnly Date    { get; set; }
    public double   WeightKg { get; set; }
}
```

### 3.2 `DailyActualWeight` Entity *(Phase 4 placeholder)*

**Path:** `src/LeanAI.Domain/WeightManagement/Entities/DailyActualWeight.cs`

```csharp
public class DailyActualWeight : BaseEntity
{
    public DateOnly Date     { get; set; }
    public double   WeightKg { get; set; }
    public string?  Notes    { get; set; }
}
```

Phase 4 adds the repository, UI, and business logic. The table exists here solely to
make the "Preserve ActualWeight on goal reset" constraint enforceable by a FK or
migration-order guarantee.

### 3.3 `IDailyIdealWeightRepository` Interface

**Path:** `src/LeanAI.Domain/WeightManagement/Interfaces/IDailyIdealWeightRepository.cs`

```csharp
public interface IDailyIdealWeightRepository
{
    Task DeleteAllAsync(CancellationToken ct = default);
    Task InsertBatchAsync(IEnumerable<DailyIdealWeight> entries, CancellationToken ct = default);
}
```

---

## 4. Application Layer — New Files

### 4.1 Command: `GenerateIdealPathCommand`

**Path:** `src/LeanAI.Application/WeightManagement/Commands/GenerateIdealPath/GenerateIdealPathCommand.cs`

```csharp
public record GenerateIdealPathCommand(
    DateOnly    StartDate,
    double      StartingWeightKg,
    double      TargetWeightKg,
    TargetPeriod TargetPeriod
) : IRequest<Unit>;
```

### 4.2 Handler: `GenerateIdealPathCommandHandler`

**Path:** `src/LeanAI.Application/WeightManagement/Commands/GenerateIdealPath/GenerateIdealPathCommandHandler.cs`

```
Handler steps:
1. totalDays  = request.TargetPeriod.TotalDays()
2. dailyLoss  = (request.StartingWeightKg - request.TargetWeightKg) / totalDays
3. entries    = [for i in 0..totalDays-1]:
                    new DailyIdealWeight
                    {
                        Date     = request.StartDate.AddDays(i),
                        WeightKg = request.StartingWeightKg - dailyLoss * i
                    }
4. await repository.DeleteAllAsync()      ← resets any prior ideal path
5. await repository.InsertBatchAsync(entries)
6. return Unit.Value
```

---

## 5. Infrastructure Layer — New and Modified Files

### 5.1 `DailyIdealWeightRepository`

**Path:** `src/LeanAI.Infrastructure/Repositories/DailyIdealWeightRepository.cs`

- `DeleteAllAsync`: `context.Database.ExecuteSqlRawAsync("DELETE FROM DailyIdealWeights", ct)`
- `InsertBatchAsync`: Begin EF Core transaction → `AddRange` → `SaveChangesAsync` → Commit

### 5.2 EF Core Migration

New migration: `Add_DailyIdealWeights_DailyActualWeights`

| Table | Columns |
|---|---|
| `DailyIdealWeights` | `Id TEXT PK`, `Date TEXT NOT NULL`, `WeightKg REAL NOT NULL` |
| `DailyActualWeights` | `Id TEXT PK`, `Date TEXT NOT NULL`, `WeightKg REAL NOT NULL`, `Notes TEXT` |

SQLite stores `DateOnly` as `TEXT` (ISO-8601) via EF Core value converter.

---

## 6. Files to Modify

### 6.1 `LeanAIDbContext.cs`

Add `DbSet<DailyIdealWeight>` and `DbSet<DailyActualWeight>` properties with minimal
model configuration (key, column types, `Ignore(DomainEvents)`).

### 6.2 `DependencyInjection.cs`

```csharp
services.AddScoped<IDailyIdealWeightRepository, DailyIdealWeightRepository>();
```

### 6.3 `WizardViewModel.cs`

In `Next()`, after `SaveCurrentStateAsync()` on Step 3, fire-and-forget the generation:

```csharp
_ = Task.Run(async () =>
{
    try
    {
        await _mediator.Send(new GenerateIdealPathCommand(
            StartDate:        DateOnly.FromDateTime(DateTime.Today),
            StartingWeightKg: ParseWeightToKg(StartingWeightText)!.Value,
            TargetWeightKg:   ParseWeightToKg(TargetWeightText)!.Value,
            TargetPeriod:     TargetPeriod!.Value));
    }
    catch { /* Non-fatal — Phase 5 will detect missing entries and surface an error */ }
});
```

This fires for both first-run and re-run completions (both share the same `Next()` path).
The handler's `DeleteAllAsync` call at step 4 ensures re-runs reset the previous path.

**Future settings screen (Phase 4+):** No architecture changes needed — any ViewModel can
send `GenerateIdealPathCommand` via `IMediator` once the screen is built.

---

## 7. TDD Test Plan

File: `tests/LeanAI.Tests/Application/WeightManagement/GenerateIdealPathCommandHandlerTests.cs`

All tests mock `IDailyIdealWeightRepository` via Moq and use FluentAssertions.

| # | Test Name | What it verifies |
|---|---|---|
| 1 | `ThreeMonths_Generates90Entries` | `InsertBatchAsync` receives exactly 90 entries |
| 2 | `SixMonths_Generates180Entries` | `InsertBatchAsync` receives exactly 180 entries |
| 3 | `OneYear_Generates365Entries` | `InsertBatchAsync` receives exactly 365 entries |
| 4 | `Entries_HaveCorrectDates` | First entry = StartDate, last entry = StartDate + 89 (for 90-day period) |
| 5 | `Entries_HaveCorrectWeights` | Day 0 = StartingWeightKg; Day i = StartingWeightKg − DailyLoss × i |
| 6 | `DeleteAll_CalledBeforeInsert` | Verify call order via Moq `MockSequence` or ordered `Callback` |

---

## 8. Definition of Done

- [x] `DailyIdealWeight` entity in Domain
- [x] `DailyActualWeight` entity in Domain (placeholder)
- [x] `IDailyIdealWeightRepository` interface in Domain
- [x] `GenerateIdealPathCommand` + `GenerateIdealPathCommandHandler` in Application
- [x] `DailyIdealWeightRepository` in Infrastructure (with transaction)
- [x] `LeanAIDbContext` updated with both new `DbSet`s
- [x] EF Core migration applied cleanly (no data loss on existing DB)
- [x] `DependencyInjection.cs` registers the new repository
- [x] `WizardViewModel.Next()` fires command fire-and-forget on Step 3 completion
- [x] Application-layer tests pass with 100% branch coverage (6 tests)
- [x] `dotnet build` → 0 errors, 0 warnings
- [x] `dotnet test` → 44/44 green (38 pre-existing + 6 new)
