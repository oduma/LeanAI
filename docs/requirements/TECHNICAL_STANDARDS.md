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
- **AI (text):** `Microsoft.Extensions.AI` — all text-based Gemini calls use `IChatClient.GetResponseAsync` with a `ChatRole.System` instruction and a `ChatRole.User` prompt. The client is resolved from DI; the API key and model name are held in `GeminiKeyHolder` and injected at registration time.
- **AI (vision / multimodal):** `Microsoft.Extensions.AI` — for image-based Gemini calls, construct the `ChatRole.User` message as an `AIContent[]` array containing a `DataContent(byte[] imageBytes, string mimeType)` followed by a `TextContent` prompt. The same `IChatClient` abstraction handles both text and vision; no additional packages are required.
- **Testing:** `xUnit`, `Moq`, `FluentAssertions`.

## 6. Platform Entry Points (Android)
- **Share-sheet intents:** A native Android `Activity` (extending `Activity` directly, not `MauiAppCompatActivity`) is registered via `[IntentFilter]` attributes. The MAUI host and DI container are already initialised by `MainApplication` before any Activity's `OnCreate` runs, so services are resolved via `IPlatformApplication.Current!.Services.GetRequiredService<T>()`.
- **Async dispatch:** The Activity wraps the mediator `Send` call in `Task.Run` to avoid blocking the UI thread, and uses `RunOnUiThread` to post Toast notifications and call `Finish()` back on the main thread.
- **`[IntentFilter]` attributes** generate the required `AndroidManifest.xml` entries automatically at build time — do not add intent filters manually to the manifest.
- **Fresh-process Gemini key provisioning:** When a share-sheet Activity starts in a fresh process (the MAUI main window was never opened), `App.xaml.cs`'s `ProvisionAiSettingsAsync` is never called and `GeminiKeyHolder.ApiKey` stays empty, causing every Gemini call to fail. The Activity must read the key from `SecureStorage` (key `"gemini_key"`, matching `App.xaml.cs`) and set it on the singleton `GeminiKeyHolder` inside `Task.Run` before dispatching any command.
- **Loading screen:** A plain `Activity` shows a blank window if `SetContentView` is never called. Call `SetContentView` at the start of `OnCreate` with a programmatic layout (dark background, branded spinner, status label) so the user sees a meaningful UI during the async AI call. Use the LeanAI palette constants directly: `ColorBase #222222`, `ColorCopper #D28B5C`, `ColorNickel #9A9EAB`, `ColorText #F0F2F5`.
- **`GetParcelableExtra` deprecation:** The generic overload does not exist in .NET Android bindings. Use the non-generic version with an `as` cast (`Intent?.GetParcelableExtra(key) as Android.Net.Uri`) and suppress `CA1422` with `#pragma warning disable/restore`.

## 7. Implementation
- **C# 14/15 Features:** Use Primary Constructors, Record types for DTOs, and Raw String Literals for AI prompts.
- **Background Tasks:** Use `Task.Run` within the Application/Infrastructure boundary to ensure the UI remains at 60fps.