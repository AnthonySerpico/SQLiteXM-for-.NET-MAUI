# SQLiteXM Versioning Policy

SQLiteXM uses a three-part version number:

**MAJOR.MINOR.PATCH**

For example:

```text
1.4.7
```

Each part of the version communicates the type of change included in a release.

## Version Components

### MAJOR

The **MAJOR** version is incremented when a release contains a **breaking change**.

A breaking change is any change in SQLiteXM that **can require an existing user to make a change in their use of the library**. The word "can" is deliberate. A change is breaking because of what the change does. Whether it actually breaks any particular user depends on whether that user's code is affected.

This is not limited to changes that cause existing code to stop compiling. A change may also be considered breaking if an existing user must change their code, configuration, initialization, handling of results or errors, or other usage of SQLiteXM in order to continue correctly using the library.

When a MAJOR version is incremented, the MINOR and PATCH versions are reset to zero.

For example:

```text
1.7.3 → 2.0.0
```

### MINOR

The **MINOR** version is incremented when a release adds new functionality without introducing a breaking change.

This includes new features, capabilities, APIs, or other additions that existing users can continue to use without modifying their existing SQLiteXM usage.

When the MINOR version is incremented, the PATCH version is reset to zero.

For example:

```text
1.7.3 → 1.8.0
```

### PATCH

The **PATCH** version is incremented when a release contains bug fixes or other corrections that do not introduce a breaking change or add a new feature.

For example:

```text
1.7.3 → 1.7.4
```

## Multiple Types of Changes

A release may contain more than one type of change. The version number is determined by the **highest level of change included in the release**.

### Bug Fixes Only

Bug fixes increment the PATCH version:

```text
1.4.7 → 1.4.8
```

### Features and Bug Fixes

If a release contains both new features and bug fixes, the MINOR version is incremented and PATCH is reset to zero:

```text
1.4.7 → 1.5.0
```

### Breaking Changes

If a release contains a breaking change, the MAJOR version is incremented and both MINOR and PATCH are reset to zero.

This applies even when the same release also contains new features and bug fixes:

```text
1.4.7 → 2.0.0
```

## Summary

| Change Included in Release                  | Version Change                 |
| ------------------------------------------- | ------------------------------ |
| Bug fix                                     | `PATCH + 1`                    |
| New feature                                 | `MINOR + 1`, PATCH → `0`       |
| New feature + bug fixes                     | `MINOR + 1`, PATCH → `0`       |
| Breaking change                             | `MAJOR + 1`, MINOR/PATCH → `0` |
| Breaking change + features and/or bug fixes | `MAJOR + 1`, MINOR/PATCH → `0` |

In other words, SQLiteXM follows this progression:

```text
PATCH
  ↓
MINOR
  ↓
MAJOR
```

A higher-level change takes precedence over lower-level changes when determining the version of a release.

## Examples

Starting with version `1.2.3`:

```text
Bug fix
1.2.3 → 1.2.4

Another bug fix
1.2.4 → 1.2.5

New feature
1.2.5 → 1.3.0

New feature + bug fixes
1.3.0 → 1.4.0

Breaking change
1.4.0 → 2.0.0

Breaking change + new features + bug fixes
2.0.0 → 3.0.0
```

## The Purpose of Versioning

SQLiteXM's version number is intended to help users understand the potential impact of upgrading.

In particular, the MAJOR version communicates whether an upgrade **may require existing users to change how they use SQLiteXM**.

Users should therefore pay particular attention to MAJOR version changes and consult the release notes for details about any breaking changes.

The version number describes the **released library**, not the number of changes or commits made during development. Multiple changes may be included in a single release, and the version is assigned according to the most significant type of change included in that release.
