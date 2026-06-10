# Defining Your Data

> 📖 **Guide Status**: Coming soon  
> This guide will cover entities, attributes, indexes, foreign keys, and schema migrations.

## Quick Preview

Topics covered in this guide:

### Entities & Attributes
- Inheriting from `SxmEntity`
- Property mapping and `[Column]` attribute
- `[NotColumn]` for computed properties
- `IsColumnAttributeRequired` mode

### Primary Keys & Auto-Increment
- Default `id` field (INTEGER PRIMARY KEY)
- Custom primary keys
- `SynchId` GUID for sync scenarios

### Indexes & Compound Indexes
- `[CreateIndex]` for single-column indexes
- `[CreateUniqueIndex]` for uniqueness constraints
- Compound indexes in `statements.json`
- When to use indexes

### Required Fields & Defaults
- `[RequiredNotNull]` attribute
- Default values
- Nullable vs. non-nullable types

### Schema Migrations
- Automatic column addition
- `[RenamedFrom]` for column renames
- Handling breaking changes
- Version management

---

**For now**, see the [Getting Started guide](GettingStarted.md) for basic entity examples.

Want to contribute? This guide needs expansion! See [CONTRIBUTING.md](../CONTRIBUTING.md).
