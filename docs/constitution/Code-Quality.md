# Code Quality Standards (SOLID, DRY, KISS)

## 1. SOLID Principles
- **Single Responsibility:** Classes should do one thing. A `WeightParser` should not also save to the database.
- **Open/Closed:** Use interfaces and abstract classes to allow for new fitness data sources without modifying existing parsing logic.
- **Liskov Substitution:** Derived classes must be usable through their base interfaces without side effects.
- **Interface Segregation:** Create small, specific interfaces (e.g., `IWeightSource`, `IRunSource`) rather than one giant `IFitnessData` interface.
- **Dependency Inversion:** High-level modules must not depend on low-level modules. Both must depend on abstractions.

## 2. DRY & KISS
- **DRY:** Abstract common math (e.g., Unit Conversions for KG to LB) into a Domain Service.
- **KISS:** Prefer readable code over "clever" one-liners. If a logic flow is complex, break it into smaller, named methods that describe the intent.

## 3. C# Specifics
- Use `readonly` for dependencies injected via constructor.
- Use `record` types for DTOs and immutable Value Objects.
- Use File-Scoped Namespaces to reduce indentation.