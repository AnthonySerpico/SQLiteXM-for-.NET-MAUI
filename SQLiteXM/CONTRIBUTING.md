# Contributing Guidelines

## Overview

This project values clear documentation, predictable APIs, and consistent coding practices. These guidelines cover documentation requirements, naming conventions, SQL generation rules, testing expectations, and workflow guidance.

## Repository structure

- `SQLiteXM.sln` is the root solution file.
- `SQLiteXM/` contains the library source.
- `SQLiteXM/SQLiteXM.csproj` defines the library project.
- `SQLiteXM.Tests/SQLiteXM.Tests.csproj` contains unit and integration tests for the library.
- `Samples/DirectBindingDemo/DirectBindingDemo.csproj`, `Samples/RegistrationDemo/RegistrationDemo.csproj`, and `Samples/QueryGalleryDemo/QueryGalleryDemo.csproj` contain the MAUI sample applications.
- `Samples/` should be used for end-to-end usage examples.
- Keep library changes in `SQLiteXM/` unless the change is sample-specific.
- Keep sample-only changes out of the core library unless they are required to demonstrate or validate new behavior.

## Contribution workflow

- Keep changes focused and small.
- Update or add tests for behavior changes.
- Update documentation when public behavior or usage changes.
- Verify that the solution builds and relevant tests pass before submitting a change.
- Prefer additive changes over breaking changes unless a breaking change is required and documented.

## Feature proposals

- Open an issue before implementing major features.
- Discuss design and scope first for changes that affect public APIs, schema behavior, or core architecture.
- Small bug fixes and documentation improvements do not need prior discussion.

## How to build the project

- Open `SQLiteXM.sln` in Visual Studio and build the solution.
- Or use the .NET CLI from the repository root:

```powershell
dotnet build SQLiteXM.sln
```

- The core library source lives under `SQLiteXM/`, and the project is defined by `SQLiteXM/SQLiteXM.csproj`.
- The test project source lives under `SQLiteXM.Tests/`, and the test project is defined by `SQLiteXM.Tests/SQLiteXM.Tests.csproj`.
- The sample projects are `Samples/DirectBindingDemo/DirectBindingDemo.csproj`, `Samples/RegistrationDemo/RegistrationDemo.csproj`, and `Samples/QueryGalleryDemo/QueryGalleryDemo.csproj`.
- The shared library code lives in `SQLiteXM/` and is the first place to check for runtime behavior changes.
- If you change MAUI sample code, build the affected sample project as well.
- If a change affects a specific target framework, verify that target builds cleanly.

## How to run tests

- Run the test project in Visual Studio Test Explorer, or use the .NET CLI from the repository root:

```powershell
dotnet test SQLiteXM.sln
```

- The primary automated test project is `SQLiteXM.Tests/SQLiteXM.Tests.csproj`.
- Use `SQLiteXM.sln` for solution-wide test discovery in Visual Studio.
- If you are working on a focused change, run the narrowest relevant test subset first.
- Re-run the affected tests after fixing any failures to confirm the behavior is stable.

## Documentation (XML comments)

- All public and internal types, members, properties, methods, and constructors MUST include XML documentation comments using triple-slash syntax (`///`).
- Every XML comment block MUST include a `<summary>` element with a concise sentence describing the member. Sentences should be sentence-case and end with a period.
- Methods and constructors MUST include `<param name="...">` elements for each parameter and a `<returns>` element when the method returns a value other than `void`.
- Properties SHOULD include a `<value>` element when additional context about the returned value is helpful.
- Use `<remarks>` for non-obvious behavior, thread-safety, performance considerations, side effects, and exception behavior.
- Use `<see cref="T:Namespace.Type"/>` to reference other types or members where appropriate.
- Leave a single blank line between the XML comment block and the member declaration.

Example:

```csharp
/// <summary>
/// Creates a new <see cref="MyType"/> instance with the supplied name.
/// </summary>
/// <param name="name">The name to assign to the instance.</param>
/// <returns>The created instance.</returns>
public MyType(string name) { ... }
```

## Naming conventions

- Public and internal symbols (types, methods, properties, events, fields, delegates) MUST use PascalCase.
- Properties MUST use PascalCase.
- Enum values MUST use PascalCase.
- Private fields MUST use `_camelCase` (leading underscore followed by camelCase).
- Method names MUST use PascalCase regardless of accessibility (public, internal, private). Asynchronous methods SHOULD end with the `Async` suffix.
- Type parameters should be a single letter or PascalCase starting with `T`.
- Local variables and method parameters MUST use camelCase.

## Required tooling and formatting

- Follow the project `.editorconfig` exactly for formatting, naming, and analyzer settings.
- Address analyzer warnings related to public/internal API documentation (CS1591) before merging.
- Keep nullable reference type annotations consistent with surrounding code.
- Prefer `async`/`await` over synchronous blocking when possible.
- Prefer `using` / `await using` for disposable resources when supported.

## Build and test validation

- Build the solution before submitting changes.
- Run the library test project after changing core behavior.
- Run or at least validate the affected MAUI sample when the change affects startup, UI binding, or sample workflows.
- If a change affects only one area, run the narrowest relevant test set first and broaden if needed.
- Do not rely on manual testing alone when automated tests can cover the behavior.

## SQL identifier quoting and statement construction

- Always use `SxmHelpers.QuoteIdentifier(name)` when embedding database identifiers (table names, column names, index names, trigger names) into SQL text. This helper:
  - Validates input and throws `ArgumentException` for null or whitespace names.
  - Escapes embedded double quotes by doubling them and wraps the identifier in double quotes per SQL/SQLite rules.
- Do NOT perform manual or ad-hoc escaping when generating SQL identifiers.
- For PRAGMA statements that accept an identifier (for example, `PRAGMA foreign_key_list(<table>)`), prefer quoting the identifier with `SxmHelpers.QuoteIdentifier(tableName)` rather than treating it as a string literal.
- When statement parameters are values (not identifiers), use parameterized queries rather than interpolation.
- Never concatenate user input directly into SQL text.

Example:

```csharp
string pragma = $"PRAGMA foreign_key_list({SxmHelpers.QuoteIdentifier(tableName)})";
```

## Thread-safety and initialization

- Initialization that mutates shared state (mapping schemas, global caches, database descriptors, or registration state) MUST be synchronized.
- Prefer dedicated synchronization primitives such as `SemaphoreSlim` or `lock` for shared mutable state.
- Document expected concurrency behavior, initialization order, and any reset behavior in XML comments on the affected members.
- Avoid calling initialization routines concurrently unless the API explicitly supports it.

## MAUI and sample-app guidance

- Keep MAUI-specific code in the appropriate `Platforms/` folder when it targets a single platform.
- In the sample projects, platform-specific code belongs under `Samples/DirectBindingDemo/Platforms/`, `Samples/RegistrationDemo/Platforms/`, or `Samples/QueryGalleryDemo/Platforms/` as appropriate.
- Use the MAUI equivalent of any platform-specific guidance; do not introduce Xamarin.Forms terminology unless there is a direct MAUI equivalent.
- Use the sample apps to demonstrate patterns rather than hiding production behavior in sample-only code.
- When a change affects app startup, resource loading, or binding behavior, validate the MAUI startup path.

## Tests and stability

- Add unit tests for helpers such as `SxmHelpers.QuoteIdentifier`, including embedded double quotes and whitespace behavior.
- Add unit tests for behavior-critical code paths such as database scanning, mapping registration, and error handling.
- Add integration tests covering PRAGMA behaviors and multi-row outputs, including `foreign_key_list` for composite keys.
- Add integration tests that run against a disposable SQLite instance to validate PRAGMA parsing and mapping registration.
- Add regression tests for any bug fix unless the change is documentation-only or sample-only.
- Prefer a failing test first when fixing a behavior bug.

## Logging and diagnostics

- Include enough context in exceptions and logs to identify the database, entity type, or SQL operation.
- Prefer logging once at the boundary where an error can be acted on.
- Do not swallow exceptions silently unless the API intentionally degrades gracefully and the behavior is documented.
- Keep diagnostic messages concise but actionable.

## Compatibility and breaking changes

- Avoid breaking public APIs unless the change is explicitly required.
- Preserve existing behavior unless the change is intentionally changing it.
- Prefer additive changes, overloads, or new helpers over rewriting existing APIs.
- If a change is breaking, document it clearly in the PR and update any affected samples or tests.

## Security-sensitive coding guidance

- Preserve provider-specific exceptions (for example, `Microsoft.Data.Sqlite.SqliteException`) so callers can inspect provider metadata such as error codes and extended properties.
- Prefer checking concrete exception types such as `ex is Microsoft.Data.Sqlite.SqliteException` instead of relying on `ex.Source` string comparisons.
- Do not double-wrap provider exceptions. If the project needs a uniform exception type for public APIs, use a wrapper that preserves provider metadata and does not hide the original provider exception unless explicitly required.
- Keep fatal and cancellation exceptions rethrown unchanged; see `ExceptionHelper.IsNonWrappable`.
- Avoid logging secrets, passwords, or other sensitive values.
- Use ViewModels for transient or sensitive values when direct entity binding would expose data that should not be persisted as-is.

## Miscellaneous

- Avoid scanning assemblies on every call; cache reflection results where appropriate and handle `ReflectionTypeLoadException` gracefully.
- Prefer explicit locking or atomic operations for shared mutable state; document the strategy in XML docs.
- Prefer clear, intention-revealing names over abbreviations except where standard .NET conventions already exist.

## Review checklist before publishing

- All public and internal members have XML documentation including `<summary>`, `<param>` for parameters, and `<returns>` for non-void methods.
- No public/internal API exposes mutable static state without synchronization details in `<remarks>`.
- All new code includes unit tests or a justification in the PR.
- Any reflection or runtime registration code includes robust error handling and logging.
- The solution builds successfully and relevant tests pass.
- MAUI sample changes have been exercised on the affected platform(s) when applicable.

## Examples and best practices

- Document thread-safety guarantees or expectations in `<remarks>` on the affected members.
- Prefer explicit locking or atomic operations for shared mutable state; document the strategy in XML docs.
- Keep SQL construction focused on identifiers versus values: quote identifiers, parameterize values.
- Use samples and tests together to show real usage and verify behavior.
