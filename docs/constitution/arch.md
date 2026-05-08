# Architecture & Domain-Driven Design (DDD) Standards

## Reference Architecture
- **Template:** [Xivotec CleanArchitecture.Maui](https://github.com/XivotecGmbH/CleanArchitecture.Maui)
- **Adaptation:** Modernize for .NET 10. 
- **Core Structure:**
    - `BaseEntity` and `BaseDomainEvent` in the Domain.
    - `BaseCommandHandler` and `BaseQueryHandler` in the Application.
    - Use `MediatR` for the mediator pattern between Presentation and Application.
    - Use `AutoMapper` or `Mapping Configurations` as defined in the Xivotec sample.

## 1. Clean Architecture Layers
- **Domain:** Pure C#. Contains Entities, Value Objects, Domain Exceptions, and Repository Interfaces. Zero dependencies on other layers or frameworks (no MAUI, no SQLite).
- **Application:** Contains Use Cases (Interactors) and DTOs. Orchestrates the flow of data to and from the Domain.
- **Infrastructure:** Implementation of Repositories (SQLite), External APIs (Shared Run Data), and Device Services (GPS).
- **Presentation (MAUI):** ViewModels and Views. 

## 2. Domain-Driven Design (DDD)
- **Bounded Contexts:** Explicitly separate 'WeightManagement' from 'ActivityTracking'.
- **Entities vs. Value Objects:** 
    - Use Entities for objects with a thread of continuity (e.g., `User`).
    - Use Value Objects for descriptive aspects (e.g., `WeightValue`, `Distance`). Value Objects must be immutable.
- **Aggregates:** Identify Root Aggregates (e.g., a `DailyLog` that contains both Weight and Exercises) to maintain data consistency.

## 3. Dependency Rule
- Dependencies must only point inwards: Presentation -> Application -> Domain.
- Use Dependency Injection (Microsoft.Extensions.DependencyInjection) for all service registrations.

## 4. Asynchronuous Processing
- All I/O operations must be asynchronous.
- All AI and DB calls must return a Task, the Ui should never be blocked waiting for a response from the DB or AI.

## 5. Persistence & Migrations (EF Core)
- **Zero Data Loss:** Every schema change must be handled via EF Core Migrations. 
- **Migration Strategy:** Use `dotnet ef migrations add` for every change. 
- **Validation:** Before finishing a Phase, verify that the migration script successfully updates the existing database without dropping tables.
- **Seeding:** Ensure 'Ideal Weight' generation (Phase 3) is handled via Application logic, not hard-coded DB seeding, to keep it flexible.