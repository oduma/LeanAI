# Technical Requirements & Best Practices

## 1. Architecture
- **Base Template:** Inspired by `XivotecGmbH/CleanArchitecture.Maui`.
- **Framework:** .NET 10.0.
- **Project Structure:** 
    - `LeanAI.Domain`
    - `LeanAI.Application`
    - `LeanAI.Infrastructure`
    - `LeanAI.Maui` (Presentation)
    - `LeanAI.Tests`

## 2. Security & Privacy
- **Gemini API Key:** MUST be stored in `Microsoft.Maui.Storage.SecureStorage`. Prompt once at runtime if missing.
- **PII (Height/Weight/Age):** Sensitive data. Encrypt during persistence if performance allows, else treat as high-security fields. Local-only storage.

## 3. Data Persistence (EF Core Migrations)
- **Zero Data Loss Policy:** User data (logs, comments) MUST survive app updates.
- **Strategy:** Every schema change must have a corresponding EF Core migration script (`dotnet ef migrations add`).
- **Design-Time Gotcha:** Implement `IDesignTimeDbContextFactory` in `LeanAI.Infrastructure` to allow migration generation on the development machine.
- **Startup Logic:** Invoke `context.Database.Migrate()` on application launch in `MauiProgram.cs`.

## 4. Patterns
- **MVVM:** Use `CommunityToolkit.Mvvm` (Source Generators for `ObservableProperty` and `RelayCommand`).
- **Async Everywhere:** All I/O and AI tasks must use `Task.Run()` or `await` patterns to keep the UI at 60fps.
- **Result Pattern:** Avoid throwing exceptions for business logic; use a `Result<T>` pattern for AI validations.


## 5. External Libraries
- **Database:** `sqlite-net-pcl` (async version).
- **AI:** `Microsoft.Extensions.AI` (for standardized Gemini/LLM integration).
- **Testing:** `xUnit`, `Moq`, `FluentAssertions`.


## 6. Implementation
- **C# 14/15 Features:** Use Primary Constructors, Record types for DTOs, and Raw String Literals for AI prompts.
- **Background Tasks:** Use `Task.Run` within the Application/Infrastructure boundary to ensure the UI remains at 60fps.