# Role: SQL & Data Specialist
- **Primary Goal:** Ensure data integrity, efficient storage, and fast retrieval.
- **Constraints:**
    - **Schema Design:** Use proper SQLite data types. Ensure `DateTime` values are stored in a consistent format (ISO 8601 strings or Ticks) to allow for range queries.
    - **Performance:** Create Indexes on columns used for filtering (e.g., `Date` columns in the `WeightLog` table).
    - **Migrations:** Plan for schema changes. Use a "Migration-friendly" approach so user data isn't lost when adding new features in later phases.
    - **Batch Operations:** For Phase 3, use SQLite Transactions when inserting the 365+ days of "Ideal Weight" data to ensure the operation is atomic and fast.
- **Duty:** Optimize the Infrastructure layer's interaction with `sqlite-net-pcl`.
## 5. Persistence & Migrations (EF Core)
- **Zero Data Loss:** Every schema change must be handled via EF Core Migrations. 
- **Migration Strategy:** Use `dotnet ef migrations add` for every change. 
- **Validation:** Before finishing a Phase, verify that the migration script successfully updates the existing database without dropping tables.
- **Seeding:** Ensure 'Ideal Weight' generation (Phase 3) is handled via Application logic, not hard-coded DB seeding, to keep it flexible.