# AI Collaboration & Interaction Workflow

## 1. Interaction Protocol
- **Ask First:** Before committing to a large block of code, always ask clarifying questions if the requirements are ambiguous. do not assume that you know the answer.
- **Scope Lock:** Do not extend the scope of a task beyond what was explicitly requested. Do not add "nice-to-have" features or speculative code.

## 2. Phased Development
- **Atomic Phases:** Work must be broken down into small, logical phases.
- **Definition of Done (DoD):** A phase is only complete when:
    1. The code adheres to `ARCH.md` and `CODE_QUALITY.md`.
    2. All unit tests pass with 100% branch coverage (per `TESTING.md`).
    3. The application is in a "Runnable" state (no broken builds).
- **No Accumulation of Debt:** Do not move to Phase 2 if Phase 1 has failing tests or compiler warnings.
- **Feedback Loop:** At the end of every phase, wait for my review and "Correction" before proceeding to the next phase.

## 3. Correction & Refinement
- If I provide a correction, treat it as a "High Priority" constraint that overrides previous assumptions for that specific phase.