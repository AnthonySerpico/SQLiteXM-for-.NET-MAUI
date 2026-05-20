# SQLiteXM Test Suite

Comprehensive test coverage for the SQLiteXM library using entity-based testing approach.

## Test Statistics (Current)

- **Total Tests**: 143
- **Passing**: 142
- **Skipped**: 1
- **Failed**: 0
- **Success Rate**: 99.3%

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

## Test Coverage by Module

### 1. EntityInitializationTests.cs (13 tests)
Tests entity creation and table initialization:
- ✓ First instantiation creates table
- ✓ Second instantiation reuses table
- ✓ All data types mapped correctly
- ✓ Time types with TEXT override
- ✓ Explicit column mapping
- ✓ Index creation (single, composite, unique)
- ✓ Foreign key creation
- ✓ Trigger creation
- ✓ Required field defaults
- ✓ Concurrent initialization (thread safety)
- ✓ Deterministic schema registration

### 2. EntityCrudTests.cs (11 tests)
Tests Create, Read, Update, Delete operations:
- ✓ SaveAsync inserts new entity and populates id
- ✓ SaveAsync updates existing entity
- ✓ InsertOrUpdateAsync behavior
- ✓ DeleteAsync removes from database
- ✓ Multiple entities have unique ids
- ✓ All data types persist correctly
- ✓ Nullable fields handle nulls
- ✓ Time types as TEXT persist
- ✓ Delete non-existent entity (no-op)
- ✓ Concurrent saves don't corrupt data
- ✓ GUID handling

### 3. EntityMigrationTests.cs (18 tests)
Tests schema migration and evolution:
- ✓ Adding properties adds columns
- ✓ New entity version initializes successfully
- ✓ Adding nullable columns
- ✓ Adding columns with defaults
- ✓ Adding NOT NULL columns with defaults
- ✓ Multiple migrations in sequence
- ✓ Concurrent migration safety
- ✓ Rollback scenarios
- ✓ Migration validation and error handling

### 4. TransactionTests.cs (5 tests)
Tests transaction support:
- ✓ SaveAsync with transaction commits
- ✓ Rollback prevents persistence
- ✓ Multiple operations are atomic
- ✓ DeleteAsync with transaction
- ✓ Ambient transaction support

### 5. LinqContextTests.cs (7 tests)
Tests basic LINQ query capabilities:
- ✓ GetTable returns queryable
- ✓ Where clause filters
- ✓ OrderBy sorts results
- ✓ Select projects properties
- ✓ FirstOrDefault returns single entity
- ✓ Count returns correct number
- ✓ Complex filters work correctly

### 6. AdvancedLinqTests.cs (12 tests)
Tests advanced LINQ patterns:
- ✓ InsertOnSubmit deferred execution
- ✓ DeleteOnSubmit deferred execution
- ✓ GroupBy with aggregates (Count, Sum, Average, Max, Min)
- ✓ Join operations (inner and left)
- ✓ Set operations (Union, Intersect, Except)
- ✓ Complex predicates and subqueries
- ✓ Projection and anonymous types
- ✓ Deferred vs immediate execution

### 7. BulkLinqOperationsTests.cs (12 tests)
Tests bulk update and delete operations:
- ✓ Bulk update single property
- ✓ Bulk update multiple properties
- ✓ Bulk update with complex predicates
- ✓ Bulk update within transactions
- ✓ Bulk delete with filters
- ✓ Bulk delete with complex conditions
- ✓ Bulk delete within transactions
- ✓ Chained bulk operations
- ✓ Performance validation

### 8. LinqTransactionTests.cs (6 tests)
Tests LINQ with transactions:
- ✓ LINQ query after rollback shows no changes
- ✓ LINQ query after commit shows changes
- ✓ Deferred operations with transaction rollback
- ✓ Deferred operations with transaction commit
- ✓ Mixed deferred and immediate operations
- ✓ Transaction isolation levels

### 9. EntityMappingTests.cs (4 tests)
Tests property mapping utilities:
- ✓ MapProperties copies matching properties
- ✓ MapAndSaveAsync maps and persists
- ✓ Null source throws ArgumentNullException
- ✓ Mismatched types skip property

### 10. ColumnRenameTests.cs (10 tests)
Tests column rename functionality:
- ✓ Single-step rename preserves data
- ✓ Multi-step rename chain
- ✓ Rename with data type changes
- ✓ Rename validation and error handling
- ✓ Concurrent rename safety
- ✓ [Rename] attribute processing

### 11. DropTableTests.cs (22 tests)
Tests table drop functionality:
- ✓ Drop non-existent table (no-op)
- ✓ Drop empty table
- ✓ Drop table with data
- ✓ Drop parent table with foreign keys (force flag)
- ✓ Drop child table first (cascade)
- ✓ Drop multiple tables
- ✓ Drop and recreate scenarios
- ✓ Error handling and validation

### 12. ConnectionManagerWorkerTests.cs (7 tests)
Tests connection pooling and worker patterns:
- ✓ RunWorkersAsync with concurrent workers
- ✓ Worker lease management
- ✓ Lock contention handling
- ✓ Timeout behaviors
- ✓ Deterministic cleanup
- ✓ Worker error propagation

### 13. SharedConnectionTests.cs (7 tests)
Tests shared connection behavior:
- ✓ Concurrent access with locking
- ✓ Lock acquisition and release
- ✓ Timeout scenarios
- ✓ Multiple callers on shared connection
- ✓ Connection state management
- ✓ Thread safety validation

### 14. FailFastTests.cs (5 tests)
Tests fail-fast validation:
- ✓ RegisterSchemaAsync with non-entity type throws
- ✓ RegisterSchemaAsync with abstract type throws
- ✓ Entity constructor without registration throws
- ✓ Invalid entity configuration detection
- ✓ Early validation error messages

### 15. SubmitChangesRefactorTests.cs (4 tests)
Tests SubmitChanges result handling:
- ✓ AllSucceeded returns correct summary
- ✓ ThrowIfFailed behavior
- ✓ Manual result inspection
- ✓ Error aggregation and reporting

## Running the Tests

### Visual Studio
1. Open Test Explorer (Test → Test Explorer)
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

## Key Testing Principles

### ✓ Correct Approach
```csharp
// Entity-based (SQLiteXM way)
var entity = new SimpleEntity { Name = "Test" };
await entity.SaveAsync();
```

### ✗ Incorrect Approach
```csharp
// Raw SQL (not how SQLiteXM works)
await connection.ExecuteNonQueryAsync(
    "CREATE TABLE SimpleEntity...", null);
```

## Coverage Summary

| Component | Coverage | Notes |
|-----------|----------|-------|
| Entity Initialization | ✓ Complete | All attribute types tested |
| CRUD Operations | ✓ Complete | Insert, Update, Delete tested |
| Data Type Mapping | ✓ Complete | All C# → SQLite types |
| Time Type Overrides | ✓ Complete | INTEGER → TEXT tested |
| Indexes | ✓ Complete | Single, composite, unique |
| Foreign Keys | ✓ Complete | Relationship creation |
| Triggers | ✓ Complete | Trigger creation |
| Transactions | ✓ Complete | Commit, rollback, ambient |
| Basic LINQ Queries | ✓ Complete | Where, OrderBy, Select, etc. |
| Advanced LINQ | ✓ Complete | GroupBy, Join, aggregates |
| Bulk Operations | ✓ Complete | Bulk update/delete via LINQ |
| Property Mapping | ✓ Complete | MapProperties, MapAndSave |
| Concurrent Access | ✓ Complete | Thread-safe initialization |
| Connection Pooling | ✓ Complete | Worker patterns, locking |
| Shared Connections | ✓ Complete | Lock contention, timeouts |
| Schema Migration | ✓ Complete | Column addition, rename |
| Column Rename | ✓ Complete | [Rename] attribute |
| Table Drop | ✓ Complete | Force drop, cascades |
| Fail-Fast Validation | ✓ Complete | Early error detection |
| SubmitChanges API | ✓ Complete | Result handling, errors |

## Test Organization

Tests are organized into collections for sequential or parallel execution:
- **[Collection("Sequential")]** - Tests that must run sequentially (shared state)
- **[Collection("SQLiteXM Tests")]** - Tests that can run in parallel

This ensures:
- **Isolation**: Each test uses a unique database
- **Performance**: Parallel execution where safe
- **Determinism**: Sequential execution where needed

## What's Tested

### Core Entity Operations
- ✓ Entity lifecycle (create, read, update, delete)
- ✓ All C# data types (int, string, bool, DateTime, Guid, etc.)
- ✓ Nullable fields and required fields
- ✓ Custom column attributes ([Column], [Rename])
- ✓ Indexes (single, composite, unique)
- ✓ Foreign key relationships
- ✓ Database triggers
- ✓ Concurrent entity operations

### LINQ Support
- ✓ Basic queries (Where, OrderBy, Select, Count, FirstOrDefault)
- ✓ Advanced queries (GroupBy, Join, Union, Intersect, Except)
- ✓ Aggregates (Count, Sum, Average, Min, Max)
- ✓ Deferred execution (InsertOnSubmit, DeleteOnSubmit)
- ✓ Bulk operations (Set().UpdateAsync(), DeleteAsync())
- ✓ Query composition and subqueries

### Transaction Management
- ✓ Manual transactions (commit, rollback)
- ✓ Ambient transactions
- ✓ Transaction with LINQ queries
- ✓ Transaction isolation
- ✓ Nested transaction behavior

### Schema Management
- ✓ Automatic table creation
- ✓ Schema migration (adding columns)
- ✓ Column rename with [Rename] attribute
- ✓ Table drop with force flag
- ✓ Foreign key handling on drop
- ✓ Deterministic schema registration

### Connection Management
- ✓ Shared connections with locking
- ✓ Connection pooling with workers
- ✓ Lock acquisition and timeout
- ✓ Concurrent access safety
- ✓ Worker lease management

### Error Handling
- ✓ Fail-fast validation on registration
- ✓ SubmitChanges result inspection
- ✓ Transaction error handling
- ✓ Migration error scenarios

## What's NOT Tested (Future Coverage)

- ⏳ Very large datasets (performance benchmarks)
- ⏳ Complex multi-level foreign key cascades
- ⏳ Trigger execution verification (beyond creation)
- ⏳ Index performance validation
- ⏳ Custom column type converters
- ⏳ Database corruption recovery
- ⏳ Disk full scenarios
- ⏳ Connection pool exhaustion

## Test Best Practices Used

1. **Isolation** - Each test uses unique database via TestBase
2. **Cleanup** - Automatic disposal after each test
3. **Naming** - Clear Given_When_Then or descriptive names
4. **Assertions** - FluentAssertions for readability
5. **Async** - All tests use async/await properly
6. **Entity-Based** - Following SQLiteXM's design philosophy
7. **Collections** - Sequential vs parallel execution control
8. **Determinism** - Unique identifiers to avoid test interference


## Troubleshooting

### Tests Failing?
1. Ensure SQLiteXM project builds successfully
2. Check that temp directory is writable
3. Verify all NuGet packages are restored
4. Run `dotnet clean` followed by `dotnet build`

### Slow Tests?
- Tests create unique databases per instance (file I/O overhead)
- Some tests use `Task.Delay` to test timing scenarios
- Consider running specific test classes instead of all tests
- xUnit runs tests in parallel by default (check collections)

### Connection Timeout Errors?
- Some tests intentionally test timeout scenarios (expected behavior)
- Check ConnectionManagerWorkerTests and SharedConnectionTests
- These tests verify lock contention handling

## Contributing New Tests

When adding new tests:

1. **Inherit from TestBase** for automatic database management
2. **Use unique identifiers** (GUIDs) to prevent test interference
3. **Add [Collection] attribute** appropriately:
   - Use `[Collection("Sequential")]` if tests share state
   - Use `[Collection("SQLiteXM Tests")]` for isolated parallel tests
4. **Follow naming conventions**: `MethodName_Scenario_ExpectedResult`
5. **Use FluentAssertions** for readable assertions
6. **Clean up properly** - TestBase handles this, but explicit cleanup is fine
7. **Test entity-based operations** - avoid raw SQL unless testing SQL features

Example:
```csharp
[Collection("Sequential")]
public class MyNewTests : TestBase
{
    [Fact]
    public async Task SaveAsync_WithUniqueConstraint_ShouldEnforceUniqueness()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        var entity1 = new UniqueEntity { Code = "ABC123" };
        await entity1.SaveAsync();

        // Act
        var entity2 = new UniqueEntity { Code = "ABC123" };
        Func<Task> act = async () => await entity2.SaveAsync();

        // Assert
        await act.Should().ThrowAsync<SqliteException>();
    }
}
```

## Test Execution Details

### Sequential Tests
Tests marked with `[Collection("Sequential")]` run one at a time to:
- Test concurrent access patterns safely
- Avoid database lock contention between tests
- Validate transaction isolation properly

### Parallel Tests
Tests marked with `[Collection("SQLiteXM Tests")]` run in parallel because:
- Each test uses a unique database file
- No shared state between tests
- Faster overall execution

## Documentation

For more information about SQLiteXM:
- **Main README**: `../README.md`
- **Usage Patterns**: `../SQLITEXM_USAGE_PATTERNS.md`
- **Cheat Sheet**: `../SQLITEXM_CHEATSHEET.md`
- **SubmitChanges API**: `../SUBMITCHANGES_API_SUMMARY.md`
- **Contributing Guide**: `../SQLiteXMCL/CONTRIBUTING.md`

## Test Suite Maintenance

This README should be updated when:
- New test classes are added
- Test counts change significantly
- New features are tested
- Coverage gaps are identified
- Test infrastructure changes

**Last Updated**: Current as of 143 tests (142 passing, 1 skipped)

