# SQLiteXM Test Suite

Comprehensive test coverage for the SQLiteXM library using entity-based testing approach.

## Test Structure

### TestBase.cs
Base class providing:
- Automatic test database creation with unique names per test
- SQL statements file generation
- SQLiteXM initialization helper
- Automatic cleanup after each test

### TestEntities.cs
Test entity classes covering:
- **SimpleEntity** - Basic entity with string, int, bool
- **AllTypesEntity** - All supported C# data types
- **TimeTypeTextEntity** - Time types overridden to TEXT storage
- **ExplicitColumnEntity** - Testing `[Column]` attribute requirement
- **IndexedEntity** - Composite and unique indexes
- **ParentEntity/ChildEntity** - Foreign key relationships
- **TriggerEntity** - Database triggers
- **RequiredFieldEntity** - `[RequiredNotNull]` attribute

## Test Coverage

### 1. EntityInitializationTests.cs (11 tests)
Tests entity creation and table initialization:
- ? First instantiation creates table
- ? Second instantiation reuses table
- ? All data types mapped correctly
- ? Time types with TEXT override
- ? Explicit column mapping
- ? Index creation
- ? Foreign key creation
- ? Trigger creation
- ? Required field defaults
- ? Concurrent initialization (thread safety)

### 2. EntityCrudTests.cs (12 tests)
Tests Create, Read, Update, Delete operations:
- ? SaveAsync inserts new entity and populates id
- ? SaveAsync updates existing entity
- ? InsertOrUpdateAsync behavior
- ? DeleteAsync removes from database
- ? Multiple entities have unique ids
- ? All data types persist correctly
- ? Nullable fields handle nulls
- ? Time types as TEXT persist
- ? Delete non-existent entity (no-op)
- ? Concurrent saves don\'t corrupt data

### 3. EntityMigrationTests.cs (2 tests)
Tests schema migration:
- ? Adding properties adds columns
- ? New entity version initializes successfully

### 4. TransactionTests.cs (5 tests)
Tests transaction support:
- ? SaveAsync with transaction commits
- ? Rollback prevents persistence
- ? Multiple operations are atomic
- ? DeleteAsync with transaction
- ? Ambient transaction support

### 5. LinqContextTests.cs (7 tests)
Tests LINQ query capabilities:
- ? GetTable returns queryable
- ? Where clause filters
- ? OrderBy sorts results
- ? Select projects properties
- ? FirstOrDefault returns single entity
- ? Count returns correct number
- ? Complex filters work correctly

### 6. EntityMappingTests.cs (4 tests)
Tests property mapping:
- ? MapProperties copies matching properties
- ? MapAndSaveAsync maps and persists
- ? Null source throws ArgumentNullException
- ? Mismatched types skip property

## Running the Tests

### Visual Studio
1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All Tests"
3. View results in Test Explorer

### Command Line
```powershell
cd C:\Users\ajser\source\repos\SQLiteXM\SQLiteXM.Tests
dotnet test
```

### With Detailed Output
```powershell
dotnet test --logger "console;verbosity=detailed"
```

### With Code Coverage
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

## Test Statistics

- **Total Tests**: 41
- **Entity-Based Tests**: 41 (100%)
- **Raw SQL Tests**: 0 (correctly avoided)

## Key Testing Principles

### ? Correct Approach
```csharp
// Entity-based (SQLiteXM way)
var entity = new SimpleEntity { Name = "Test" };
await entity.SaveAsync();
```

### ? Incorrect Approach
```csharp
// Raw SQL (not how SQLiteXM works)
await connection.ExecuteNonQueryAsync(
    "CREATE TABLE SimpleEntity...", null);
```

## Coverage Summary

| Component | Coverage | Notes |
|-----------|----------|-------|
| Entity Initialization | ? Complete | All attribute types tested |
| CRUD Operations | ? Complete | Insert, Update, Delete tested |
| Data Type Mapping | ? Complete | All C# ? SQLite types |
| Time Type Overrides | ? Complete | INTEGER ? TEXT tested |
| Indexes | ? Complete | Single, composite, unique |
| Foreign Keys | ? Complete | Relationship creation |
| Triggers | ? Complete | Trigger creation |
| Transactions | ? Complete | Commit, rollback, ambient |
| LINQ Queries | ? Complete | Where, OrderBy, Select, etc. |
| Property Mapping | ? Complete | MapProperties, MapAndSave |
| Concurrent Access | ? Complete | Thread-safe initialization |
| Migration | ?? Partial | Basic column addition |

## What\'s NOT Yet Tested

- ? SxmStatement (named SQL execution)
- ? Connection pooling edge cases
- ? Complex foreign key scenarios
- ? Trigger execution verification
- ? Index performance validation
- ? Large dataset operations
- ? Error recovery scenarios
- ? Custom column type converters

## Test Best Practices Used

1. **Isolation** - Each test uses unique database
2. **Cleanup** - Automatic disposal after each test
3. **Naming** - Clear Given_When_Then pattern
4. **Assertions** - FluentAssertions for readability
5. **Async** - All tests use async/await
6. **Entity-Based** - Following SQLiteXM\'s design

## Troubleshooting

### Tests Failing?
1. Ensure SQLiteXM project builds successfully
2. Check that temp directory is writable
3. Verify all NuGet packages are restored

### Slow Tests?
- Tests create unique databases per instance
- File I/O can be slow on some systems
- Consider running tests in parallel (xUnit does this by default)

## Next Steps for Full Coverage

1. Add SxmStatement tests (named SQL execution)
2. Add connection manager stress tests
3. Add complex relationship tests (multiple FKs)
4. Add trigger execution validation tests
5. Add performance benchmarks
6. Add error scenario tests (disk full, corrupt DB, etc.)
