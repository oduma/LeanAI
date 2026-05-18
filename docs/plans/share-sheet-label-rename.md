# Share Sheet Label Rename ✅ COMPLETE

## Change

Remove the "LeanAI: " prefix from both Android Share Sheet entry point labels:

| Before | After |
|--------|-------|
| `"LeanAI: Import Run"` | `"Import Run"` |
| `"LeanAI: Import Food"` | `"Import Food"` |

## Files Changed

| File | Change |
|------|--------|
| `src/LeanAI.Maui/Platforms/Android/ImportRunActivity.cs` | `[Activity(Label = ...)]` and `[IntentFilter(..., Label = ...)]` updated (2 strings) |
| `src/LeanAI.Maui/Platforms/Android/ImportFoodActivity.cs` | Same 2 strings updated |
| `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` | All label references updated (global replace) |
| `docs/plans/phase-8-third-party-integration.md` | Code snippet + DoD updated |
| `docs/plans/phase-9-food-calories.md` | Code snippet + DoD updated |

## Definition of Done
- ✅ Android Share Sheet displays `"Import Run"` and `"Import Food"` (no `"LeanAI:"` prefix).
- ✅ `dotnet build` → 0 errors, 0 warnings.
- ✅ All 157 tests still pass.
- ✅ Docs updated to match.
