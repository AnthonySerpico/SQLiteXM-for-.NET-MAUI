# Contributing Guidelines

## Overview

This project values clear, concise documentation and consistent coding practices. These guidelines cover code documentation requirements, naming conventions, and basic contribution rules so that contributors can produce maintainable, discoverable code.

## Documentation (XML comments)
- All public and internal types, members, properties, methods, and constructors MUST include XML documentation comments using triple-slash syntax (`///`).
- Every XML comment block MUST include a `<summary>` element with a concise sentence describing the member. Sentences should be sentence-case and end with a period.
- Methods and constructors MUST include `<param name="...">` elements for each parameter and a `<returns>` element when the method returns a value other than `void`.
- Use `<remarks>` for non-obvious behavior, thread-safety, performance considerations, or side effects.
- Use `<see cref="T:Namespace.Type"/>` to reference other types or members where appropriate.
- Leave a single blank line between the XML comment block and the member declaration.

Example:
```csharp
/// <summary>
/// Creates a new <see cref="MyType"/> instance with the supplied name.
/// </summary>
/// <param name="name">The name to assign to the instance.</param>
public MyType(string name) { ... }
```

## Naming conventions
- Public and internal symbols (types, methods, properties, events, fields, delegates) MUST use PascalCase.
- Properties MUST use PascalCase.
- Enum values MUST use PascalCase.
- Private fields MUST use _camelCase (leading underscore followed by camelCase).
- Method names MUST use PascalCase regardless of accessibility (public, internal, private). Asynchronous methods SHOULD end with the `Async` suffix.
- Type parameters should be single letter or PascalCase starting with `T`.
- Local variables and method parameters SHOULD use camelCase.

### Exception: entity column properties
- To preserve compatibility with database column naming and external consumers, properties that are intended to map directly to database columns may use their literal column names when necessary.
- Specifically, the public properties named `id` and `synchId` on entity types are allowed to remain lower-case to match SQLite column names. When doing so, the property MUST include an explicit justification via a `[SuppressMessage]` attribute (or an XML <remarks> note) explaining the exception.
- This exception is narrowly scoped to these column-backed properties only. All other properties and public/internal symbols MUST follow the standard PascalCase rule.

## Methods and parameters
- Method parameters MUST use camelCase.
- Local variables SHOULD use camelCase.
- Avoid `var` unless the type is obvious from the right-hand side or when it improves readability; prefer explicit types for clarity.

## Documentation enforcement
- The project enables `CS1591` for public and internal members. Missing documentation will produce warnings.

## Tests
- Test files may use different naming (for example, underscores in test method names) and are covered by separate rules.

## Other
- Follow the repository formatting rules defined in `.editorconfig`.
- Be explicit about accessibility for top-level fields and members when clarity is helpful.


<!-- End of CONTRIBUTING.md -->