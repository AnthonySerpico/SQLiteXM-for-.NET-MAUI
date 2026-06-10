# Contributing Guidelines

## Overview

This project values clear, concise documentation and consistent coding practices. These guidelines cover code documentation requirements, naming conventions, and basic contribution rules so that contributors can produce maintainable, discoverable code.

## Documentation (XML comments)
- All public and internal types, members, properties, methods, and constructors MUST include XML documentation comments using triple-slash syntax (`///`).
- Every XML comment block MUST include a `<summary>` element with a concise sentence describing the member. Sentences should be sentence-case and end with a period.
- Methods and constructors MUST include `<param name="...">` elements for each parameter and a `<returns>` element when the method returns a value other than `void`.
- Properties SHOULD include a `<value>` element when additional context about the returned value is helpful.
- Use `<remarks>` for non-obvious behavior, thread-safety, performance considerations, or side effects.
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
- Private fields MUST use _camelCase (leading underscore followed by camelCase).
- Method names MUST use PascalCase regardless of accessibility (public, internal, private). Asynchronous methods SHOULD end with the `Async` suffix.
- Type parameters should be single letter or PascalCase starting with `T`.
- Local variables and method parameters MUST use camelCase.

## Required tooling and formatting
- Follow the project .editorconfig exactly for formatting, naming and analyzer settings.
- Address analyzer warnings related to public/internal API documentation (CS1591) before merging.

## SQL identifier quoting and statement construction
- Always use `SxmHelpers.QuoteIdentifier(name)` when embedding database identifiers (table names, column names, index names, trigger names) into SQL text. This helper:
  - Validates input and throws `ArgumentException` for null or whitespace names.
  - Escapes embedded double-quotes by doubling them and wraps the identifier in double-quotes per SQL/SQLite rules.
- Do NOT perform manual or ad-hoc escaping (for example, `Replace("'", "''")`) when generating SQL identifiers. Use the helper to ensure consistent, correct quoting across the codebase.
- For PRAGMA statements that accept an identifier (for example, `PRAGMA foreign_key_list(<table>)`) prefer quoting the identifier with `SxmHelpers.QuoteIdentifier(table)` rather than treating it as a string literal. Example:
  - `string pragma = $"PRAGMA foreign_key_list({SxmHelpers.QuoteIdentifier(tableName)})";`
- When statement parameters are values (not identifiers), use parameterized queries rather than interpolation.

## Thread-safety and initialization
- Initialization that mutates shared state (mapping schemas, global caches) MUST be synchronized. Prefer using dedicated synchronization primitives (`SemaphoreSlim`, `lock`, etc.) and document expected concurrency behavior in XML comments.

## Tests and stability
- Add unit tests for helpers such as `SxmHelpers.QuoteIdentifier`, including names with embedded double quotes and whitespace behavior.
- Add unit tests for behavior-critical code paths (database scanning, mapping registration, and error handling).
- Add integration tests covering PRAGMA behaviors and multi-row PRAGMA outputs (foreign_key_list for composite keys).
- Add integration tests that run against a disposable SQLite instance to validate PRAGMA parsing and mapping registration.

## Review checklist before publishing
- All public and internal members have XML documentation including `<summary>`, `<param>` for parameters, and `<returns>` for non-void methods.
- No public/internal API exposes mutable static state without clear synchronization documentation in `<remarks>`.
- All new code includes unit tests or a justification in the PR.
- Any reflection or runtime registration code includes robust error handling and logging.

## Examples and best practices
- Document any thread-safety guarantees or expectations in `<remarks>` on the affected members.
- Prefer explicit locking or atomic operations for shared mutable state; document the strategy in XML docs.

## Miscellaneous
- Avoid scanning assemblies on every call; cache reflection results where appropriate and handle `ReflectionTypeLoadException` gracefully.
- Prefer `using` / `await using` for disposable connections when the connection type supports `IDisposable` / `IAsyncDisposable`.

## Exception handling policy
- Preserve provider-specific exceptions (for example, `Microsoft.Data.Sqlite.SqliteException`) so callers can inspect provider metadata (error codes, extended properties) and make informed decisions (e.g. retry, constraint handling).
- Prefer checking concrete exception types (e.g., `ex is Microsoft.Data.Sqlite.SqliteException`) instead of relying on `ex.Source` string comparisons. Type checks are robust against localization and changes to the Source property.
- Do not double-wrap provider exceptions. If the project needs a uniform exception type for public APIs, use a wrapper that preserves provider metadata (for example by copying the provider error code into `SxmException.Data`) and avoid hiding the original exception type unless explicitly required.
- Keep fatal and cancellation exceptions rethrown unchanged (see `ExceptionHelper.IsNonWrappable`).
