# SQLiteXM Test Suite

Comprehensive test coverage for the SQLiteXM library using an entity-based testing approach.

## 📖 Test Filtering Guide

See the **[Test Filtering Guide](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/SQLiteXM.Tests/TEST_FILTERING_GUIDE.md)** for instruction for running tests.


## Test Statistics (Current)

- **Test classes**: 26
- **Discovered tests**: 386 per target framework
- **Target frameworks**: `net8.0` and `net9.0` (the suite is multi-targeted, so a full run executes 772 test cases)
- **Skipped tests**: 0
- **Performance-tagged tests**: 12 (`[Trait("Category", "Performance")]`)

> **Debug-only**: The test project fails the build in any configuration other than `Debug`. The suite depends on DEBUG-only APIs such as `SxmDatabase.ResetForTestingAsync()`. `BuildConfigurationCheck.cs` adds a `[ModuleInitializer]` guard that throws before any test runs if the assembly was compiled without `DEBUG`.

## Tooling

| Concern | Package |
|---------|---------|
| Test framework | xunit 2.9.3 |
| Test SDK / runner | Microsoft.NET.Test.Sdk 17.11.1, xunit.runner.visualstudio 2.8.2 |
| Assertions | FluentAssertions 7.0.0 |
| Mocking | Moq 4.20.72 |
| Coverage | coverlet.collector 6.0.4 |

`Xunit` and `FluentAssertions` are global usings for the project.

## Test Infrastructure

### TestBase.cs
Base class for nearly every test class. It provides:
- A **shared** test database (`test_database`) created once per process in a static constructor, under the OS temp folder (`SQLiteXM.Tests/<FrameworkDescription>/`)
- Generation of the `statements.json` SQL statements file and `DatabaseFolderOverride` wiring
- Static registration of 100+ test entity schemas up front
- Lifecycle helpers: `InitializeSqliteXMAsync()`, `CleanupTestDataAsync()` (DEBUG-only full reset), `CleanupTableDataAsync()` (data-only reset), `RestartSqliteXMAsync()` (simulates app restart), `ResetSchemaRegistrationFor(params Type[])`
- Assertion helpers: `VerifyEntityExistsInDbAsync<T>()`, `VerifyEntityNotInDbAsync<T>()`, `GetAllEntitiesFromDb<T>()`, `GetEntityCountFromDb<T>()`, `VerifyTableExists<T>()`
- Raw SQL/schema introspection helpers: `ExecuteNonQueryAsync()`, `ExecuteScalarAsync<T>()`, `ColumnExistsAsync()`, `GetColumnTypeAsync()`, `IsColumnNotNullAsync()`, `GetIndexesAsync()`, `GetIndexColumnsAsync()`, `TriggerExistsAsync()`, `GetTriggerSqlAsync()`, `DropTableDirectlyAsync()`

### TestEntities.cs
Shared entity classes:
- **SimpleEntity** - string, int, bool
- **AllTypesEntity** - all supported C# data types
- **TimeTypeTextEntity** - time types overridden to TEXT storage
- **ExplicitColumnEntity** - `[Column]` attribute requirement
- **IndexedEntity** - single, composite, and unique indexes
- **ParentEntity / ChildEntity** - foreign key relationships
- **TriggerEntity** - database triggers
- **RequiredFieldEntity** - `[RequiredNotNull]` defaults

Many test classes also declare their own nested entities so schema scenarios stay isolated.

### BenchmarkEntities.cs
Auto-generated schema-scale fixtures: 75 entities (`BenchEntity01`-`BenchEntity75`) x 50 columns, with a deterministic mix of single/composite/unique indexes and required-field defaults. Used only by the large-schema initialization benchmark.

### Collections
| Collection | Parallel? | Purpose |
|------------|-----------|---------|
| `Sequential` | No | Default for data-sensitive tests; `SequentialTestFixture` clears table data before the collection runs |
| `MultiDatabase` | No | Multi-database scenarios that call `ResetForTestingAsync()` |
| `InitializationPattern` | No | `StartInitialization` / `EnsureReadyAsync` tests that reset process-wide state |
| `SQLiteXM Tests` | No | Legacy marker collection, effectively unused |

## Test Coverage by Class

| Test class | Tests | Collection | Focus |
|------------|------:|------------|-------|
| `AdvancedLinqTests` | 12 | Sequential | GroupBy, joins, set operations, deferred execution, projections |
| `BulkInsertTests` | 70 | Sequential | Three bulk-insert entry points (`SxmSql`, `SxmTransaction`, LINQ extension) across shared behavior theories plus per-API transaction semantics |
| `BulkLinqOperationsTests` | 12 | Sequential | LINQ bulk update/delete, predicates, transaction scoping, chaining |
| `ColumnRenameTests` | 10 | Sequential | `[Rename]` processing, multi-step rename chains, data preservation |
| `ConnectionManagerWorkerTests` | 7 | Sequential | Worker leases, lock contention, timeouts, deterministic cleanup |
| `DropTableTests` | 24 | Sequential | Drop semantics, dependent-table refusal, drop ordering, recreate |
| `EntityCrudTests` | 9 | Sequential | Save/InsertOrUpdate/Delete, type round-tripping, nullables, concurrency |
| `EntityInitializationTests` | 13 | Sequential | Table creation, type mapping, indexes, FKs, triggers, thread safety |
| `EntityMappingTests` | 4 | Sequential | `MapProperties`, `MapAndSaveAsync`, null/mismatch handling |
| `EntityMigrationTests` | 17 | Sequential | Adding columns (nullable, defaulted, NOT NULL), sequential and concurrent migrations |
| `FailFastTests` | 5 | Sequential | Early validation on registration and entity construction |
| `InitializationPatternTests` | 11 | InitializationPattern | `SxmDatabase.StartInitialization` + `EnsureReadyAsync`, idempotency, failure propagation |
| `LargeSchemaInitializationBenchmarkTests` | 1 | InitializationPattern | Wall-clock measurement of first-run and migration-run initialization over a 75 x 50 schema (no pass/fail threshold) |
| `LinqContextTests` | 7 | Sequential | Where, OrderBy, Select, FirstOrDefault, Count |
| `LinqQueryDocumentationTests` | 43 | Sequential | Validates every pattern documented in `Docs/linq-queries.md` using Customer/Order entities |
| `LinqTransactionTests` | 6 | Sequential | LINQ visibility across commit/rollback, deferred operations, isolation |
| `MixedUnitOfWorkTests` | 5 | Sequential | Entity DML + embedded SQL + LINQ sharing one unit of work; ambient transaction joining |
| `MultiDatabaseLinqTests` | 18 | MultiDatabase | Per-database query contexts, aggregates, joins, projections |
| `MultiDatabasePerformanceTests` | 12 | MultiDatabase, `Category=Performance` | Bulk, scale, and concurrency benchmarks; update-path correctness, transaction-batching speedup ratio, and throughput reporting |
| `MultiDatabaseTests` | 11 | MultiDatabase | Multiple databases in `statements.json`, entity routing, per-database transactions |
| `NullHandlingTests` | 28 | Sequential | `NullTestEntity` with 24 nullable columns across TEXT/INTEGER/REAL/BLOB; verifies real SQL `NULL` via `typeof(column)` on insert, update, bulk insert, LINQ bulk update, and read paths |
| `SchemaEvolutionTests` | 24 | Sequential | Index add/remove/change, trigger add/remove/modify, column drops (including indexed and trigger-referenced), combined rename + drop, unsupported-change contracts |
| `SharedConnectionTests` | 7 | Sequential | Shared connection locking, timeouts, multi-caller safety |
| `SxmExceptionContractTests` | 9 | (default) | `SxmException` metadata contract: `ErrorCode`, `Data["sxmErrorCode"]`, `Context`, exception filters, wrapped vs direct throws |
| `TransactionPatternTests` | 14 | Sequential | The five transaction patterns from `Docs/application-lifecycle.md`: mixed operations, fault behavior, fault recovery, multiple commits, all-operation-type conformance |
| `TransactionTests` | 7 | Sequential | Commit/rollback, atomicity, ambient transactions, nested-create rejection |

## Running Tests

### Command Line
```powershell
cd C:\Users\ajser\source\repos\SQLiteXM\SQLiteXM.Tests
dotnet test --configuration Debug
```

### Fast loop (skip performance tests)
```powershell
dotnet test --configuration Debug --filter "Category!=Performance"
```

### Single target framework
```powershell
dotnet test --configuration Debug --framework net9.0
```

### Detailed output
```powershell
dotnet test --configuration Debug --logger "console;verbosity=detailed"
```

### Code coverage
```powershell
dotnet test --configuration Debug --collect:"XPlat Code Coverage"
```

See [TEST_FILTERING_GUIDE.md](TEST_FILTERING_GUIDE.md) for the full set of filter recipes.

## Coverage Summary

This table describes the **scope** of what each area exercises — the behaviours and variants the suite drives. It is **not** measured line or branch coverage. For that, run `dotnet test --collect:"XPlat Code Coverage"` and read the generated report. Where an area is deliberately partial, the scope column says what is left out and the detail is repeated under [What's NOT Tested](#whats-not-tested-future-coverage).

| Component | Scope | Notes |
|-----------|-------|-------|
| Entity initialization | All attribute types | Table creation, type mapping, indexes, FKs, triggers, thread safety |
| CRUD operations | All four write paths | Insert, update, delete, InsertOrUpdate |
| Data type mapping | All built-in types | Every C# type to SQLite storage class; custom converters not supported/tested |
| Time type overrides | Both storage modes | INTEGER and TEXT storage |
| Null handling | All storage classes, all write paths | Real SQL `NULL` verified via `typeof(column)` on insert, update, bulk insert, LINQ bulk update, read-back |
| Indexes | Definition, evolution, and constraint enforcement | Single, composite, unique; add/remove/modify; uniqueness actually enforced on insert and bulk insert; unique-index-over-duplicate-data failure and retry. Query-plan/performance impact not asserted |
| Foreign keys | Schema and drop ordering | Constraint creation, drop-order enforcement, three-level parent chains. `ON DELETE CASCADE` row behaviour not exercised |
| Triggers | Schema lifecycle only | Creation, removal, body modification. Trigger *execution* / firing behaviour is not verified |
| Transactions | All documented patterns | Commit, rollback, ambient, fault + recovery, multi-commit |
| Mixed unit of work | Combined operation types | Entity DML + SQL + LINQ sharing one transaction |
| Basic LINQ queries | Core operators | Where, OrderBy, Select, Count, First/Single |
| Advanced LINQ | Composite queries | GroupBy, joins, set operations, aggregates, paging, deferred execution |
| Documented LINQ patterns | Every documented example | Mirrors `Docs/linq-queries.md` one-to-one |
| Bulk insert | All three entry points | `SxmSql`, `SxmTransaction`, LINQ extension; batching, id/synchId population, rollback |
| Bulk update/delete | LINQ bulk paths | `Set().UpdateAsync()` / `DeleteAsync()`, predicates, transaction scoping |
| Schema migration | Supported changes + refusals | Column add, rename, drop; unsupported-change contracts; failed-migration retry |
| Table drop | Dependency rules | Dependency refusal, drop ordering, recreate |
| Connection management | Normal and contended paths | Worker leases, shared connections, lock contention, timeouts. Pool exhaustion not exercised |
| Fail-fast validation | Registration and construction | Early validation errors |
| Exception contract | Public metadata surface | `SxmException` error codes, `Data["sxmErrorCode"]`, context, filters |
| Initialization pattern | Startup contract | `StartInitialization` + `EnsureReadyAsync`, idempotency, concurrency, failure propagation |
| Multi-database | Routing and isolation | Multiple databases, per-database transactions, LINQ, performance |
| Performance and scale | Indicative, mostly unthresholded | Bulk, large-dataset, concurrency, memory-leak checks. Timings are environment-dependent, not guarantees |

## What's NOT Tested (Future Coverage)

Each item below corresponds to a qualified entry in the scope table above.

- **Cascading deletes (`ON DELETE CASCADE`)** — the library supports `ForeignKeyDeleteAction.Cascade` and emits the clause, but no test sets it or verifies that deleting a parent row removes its children. Multi-level FK *chains* are covered by `DropTableTests` (`GrandParent -> Parent -> Child`); what is missing is row-level cascade behaviour. Related: nothing asserts `PRAGMA foreign_keys=ON`, which cascades depend on
- **Trigger execution verification** — triggers are tested for creation, removal, and body modification, but their firing behaviour and effects are not asserted
- **Index performance validation** — indexes are verified structurally *and* behaviourally (uniqueness is enforced, violations throw), but no test asserts that a query actually *uses* an index via `EXPLAIN QUERY PLAN`
- **Custom column type converters** — only built-in type mappings are covered
- **Database corruption recovery**
- **Disk-full scenarios**
- **Connection pool exhaustion** — lock contention and timeouts are tested, but not full pool starvation

## Test Best Practices Used

1. **Shared, deterministic database** - one database per process via `TestBase`, with per-test data cleanup
2. **Non-parallel collections** - data-sensitive work is serialized rather than racing
3. **Isolated schema fixtures** - schema-mutating tests declare their own nested entities
4. **Documentation-driven tests** - LINQ, schema evolution, exception, and transaction tests mirror the files in `Docs/`
5. **FluentAssertions** for readable assertions
6. **Async throughout** - no sync-over-async in tests
7. **Entity-based** - raw SQL is used only to verify or simulate legacy schema state

## Contributing New Tests

1. **Inherit from `TestBase`** for database management and helpers.
2. **Register new entity types** in `TestBase.RegisterAllTestEntitySchemasSync()`, or register them inside the test for schema-evolution scenarios.
3. **Pick the right collection**:
   - `[Collection("Sequential")]` for ordinary data-touching tests
   - `[Collection("MultiDatabase")]` for multi-database scenarios
   - `[Collection("InitializationPattern")]` when resetting process-wide SQLiteXM state
4. **Tag long-running tests** with `[Trait("Category", "Performance")]`.
5. **Follow naming conventions**: `MethodName_Scenario_ExpectedResult`.
6. **Use FluentAssertions** and unique identifiers (GUIDs) to avoid interference.

Example:
```csharp
[Collection("Sequential")]
public class MyNewTests : TestBase
{
	[Fact]
	public async Task SaveAsync_WithUniqueConstraint_ShouldEnforceUniqueness()
	{
		// Arrange
		await CleanupTableDataAsync();
		var entity1 = new UniqueEntity { Code = "ABC123" };
		await entity1.SaveAsync();

		// Act
		var entity2 = new UniqueEntity { Code = "ABC123" };
		Func<Task> act = async () => await entity2.SaveAsync();

		// Assert
		await act.Should().ThrowAsync<SxmException>();
	}
}
```

## Troubleshooting

### Build fails immediately
The project emits an MSBuild error when `Configuration != Debug`. Switch to Debug; Release builds are intentionally unsupported for the test suite.

### `InvalidOperationException` before any test runs
`BuildConfigurationCheck` detected a non-DEBUG assembly. Clean `bin`/`obj` and rebuild in Debug.

### Tests interfering with each other
Ensure the class is in a non-parallel collection and calls `CleanupTableDataAsync()`, or `CleanupTestDataAsync()` for a full reset, during Arrange.

### Slow runs
The 12 performance tests dominate wall-clock time. Use `--filter "Category!=Performance"` during development, and `--framework net9.0` to run a single target framework.

### Connection timeout errors
`ConnectionManagerWorkerTests` and `SharedConnectionTests` intentionally provoke lock contention and timeouts; those messages are expected behavior for those tests.

### Inspecting multi-database artifacts
The project defines `KEEP_MULTI_DB_TEST_FILES`, so multi-database test databases are retained in the temp folder for inspection.
