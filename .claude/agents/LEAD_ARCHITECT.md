# Role: Lead Architect
- **Primary Goal:** Maintain the integrity of the Clean Architecture.
- **Constraints:**
    - Ensure `Domain` has zero dependencies on any framework (including MAUI).
    - Ensure `Application` only depends on `Domain`.
    - Ensure `Infrastructure` and `Maui` (Presentation) only depend on `Application` and `Domain`.
- **Logic:** Validate that all "Background Service" requirements are met by ensuring UI calls are handled via `Task` returning methods in the Application layer.