# SQLiteXM Test Suite

Comprehensive test coverage for the SQLiteXM library using an entity-based testing approach.

## 📖 Test Filtering Guide

See the **[Test Filtering Guide](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/SQLiteXM.Tests/TEST_FILTERING_GUIDE.md)** for instruction for running tests.


## Test Statistics (Current)

- **Test classes**: 40
- **Tests**: 768 per target framework
- **Target frameworks**: `net8.0` and `net10.0` (the suite is multi-targeted, so a full run executes 1536 test cases)
- **Skipped tests**: 0
- **Performance-tagged tests**: 12 (`[Trait("Category", "Performance")]`)

> **Configuration**: The test project builds and runs in any configuration, including `Release`. The reset hooks such as `SxmDatabase.ResetForTestingAsync()` are `internal`, so they do not depend on `DEBUG`.

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
| `DatabaseOptionsEffect` | No | `DatabaseOptionsEffectTests`; reinitializes SQLiteXM with different options per test |
| `NamedStatement` | No | `NamedStatementTests`; reinitializes SQLiteXM with its own `statements.json` |
| `Concurrency` | No | `ConcurrencyTests`, `ConcurrencyMultiDatabaseTests`; ordered last by `ConcurrencyLastCollectionOrderer` |

## Test Coverage by Class

| Test class | Tests | Collection | Focus |
|------------|------:|------------|-------|
| `AdvancedLinqTests` | 12 | Sequential | GroupBy, joins, set operations, deferred execution, projections |
| `BulkInsertTests` | 70 | Sequential | Three bulk-insert entry points (`SxmSql`, `SxmTransaction`, LINQ extension) across shared behavior theories plus per-API transaction semantics |
| `BulkLinqOperationsTests` | 12 | Sequential | LINQ bulk update/delete, predicates, transaction scoping, chaining |
| `ColumnRenameTests` | 10 | Sequential | `[Rename]` processing, multi-step rename chains, data preservation |
| `ConcurrencyTests` | 11 | Concurrency (custom collection orderer) | Concurrent entity and data access scenarios |
| `ConcurrencyMultiDatabaseTests` | 2 | Concurrency (custom collection orderer) | Concurrent access across multiple databases |
| `ConnectionManagerWorkerTests` | 7 | Sequential | `SxmConnectionManager.RunWorkersAsync`: concurrent workers on a shared connection, lease management, deterministic cleanup |
| `DatabaseOptionsValidatorTests` | 78 | Sequential | `SxmDatabaseOptions` validation rules: numeric ranges, undefined enum values, folder-override paths, configuration-combination warnings, and the `ValidationResult` contract. Pure in-memory - no database required. Resets the shared `EnableConnectionPooling` / `EnableLogging` / `DefaultTimeout` statics around each test |
| `DatabaseOptionsEffectTests` | 22 | DatabaseOptionsEffect | Proves the configured options actually reach SQLite: `journal_mode`, `synchronous`, `foreign_keys`, `temp_store`, `cache_size` (negative-KB encoding), `busy_timeout` and `wal_autocheckpoint` PRAGMA round-trips, plus `OnConnectionOpened` / `OnConnectionClosed` interceptor invocation and ordering. Most PRAGMAs are per-connection, so they are read from inside an interceptor on the very connection SQLiteXM configured; WAL is additionally verified from an independent connection because it is persisted in the database file. Re-initializes SQLiteXM per test and restores the standard test configuration afterwards |
| `DecimalComparisonTests` | 14 | Sequential | Decimal value comparison behavior in queries |
| `DropTableTests` | 24 | Sequential | `SxmSql.DropTableAsync`: dropping tables, foreign-key dependency refusal, drop ordering, recreate |
| `DualForeignKeyNavigationTests` | 18 | Sequential | Navigation across two foreign keys on the same entity |
| `EntityCrudTests` | 9 | Sequential | Entity create, read, update and delete |
| `EntityInitializationTests` | 13 | Sequential | Table creation, type mapping, indexes, FKs, triggers, thread safety |
| `EntityMappingTests` | 4 | Sequential | `MapProperties`, `MapAndSaveAsync`, null/mismatch handling |
| `EntityMigrationTests` | 17 | Sequential | Adding columns (nullable, defaulted, NOT NULL), sequential and concurrent migrations |
| `FailFastTests` | 5 | Sequential | Early validation on registration and entity construction |
| `GuidConsistencyTests` | 33 | Sequential | `Guid` byte order is identical on every write and read path (entity, LINQ, bulk insert, direct SQL), nullable `Guid`, `synchId`, LINQ filters, the `TEXT` override, and raw SQL parameter conversion |
| `InitializationPatternTests` | 11 | InitializationPattern | `SxmDatabase.StartInitialization` + `EnsureReadyAsync`, idempotency, failure propagation |
| `LargeSchemaInitializationBenchmarkTests` | 1 | InitializationPattern | Wall-clock measurement of first-run and migration-run initialization over a 75 x 50 schema (no pass/fail threshold) |
| `LinqContextTests` | 7 | Sequential | Where, OrderBy, Select, FirstOrDefault, Count |
| `LinqQueryDocumentationTests` | 49 | Sequential | Every LINQ example in `Docs/linq-queries.md` works as documented |
| `LinqReadFailureTests` | 9 | Sequential | A failed LINQ read marks the transaction as faulted: earlier writes roll back, later writes are skipped, commit throws, rollback resets, and joined transactions share the failure |
| `LinqTransactionTests` | 6 | Sequential | LINQ visibility across commit/rollback, deferred operations, isolation |
| `LoadWithTests` | 35 | Sequential | Eager loading via `LoadWith` navigation paths |
| `MixedUnitOfWorkTests` | 5 | Sequential | Entity writes, embedded SQL and LINQ share one unit of work; owning vs. joined ambient transactions; skipped writes after a fault |
| `MultiDatabaseLinqTests` | 18 | MultiDatabase | Per-database query contexts, aggregates, joins, projections |
| `MultiDatabasePerformanceTests` | 12 | MultiDatabase, `Category=Performance` | Bulk, scale, and concurrency benchmarks; update-path correctness, transaction-batching speedup ratio, and throughput reporting |
| `MultiDatabaseTests` | 11 | MultiDatabase | Multiple databases in `statements.json`, entity routing, per-database transactions |
| `NamedStatementTests` | 41 | NamedStatement | Executing statements by name from `statements.json`: name resolution before inline SQL, named vs. positional binding, unknown/null/empty names. Each test builds its own database and restores the standard configuration on dispose |
| `NullHandlingTests` | 28 | Sequential | Real SQL `NULL` verified via `typeof(column)` for every storage class on insert, update, bulk insert, LINQ bulk update and read; empty string/blob must not become `NULL` |
| `RunStatementTests` | 18 | Sequential | The `RunStatementAsync` overload matrix for *inline* SQL: no parameters, `Dictionary<string, object?>` named parameters, and `List<object>` positional parameters, in both the typed `TResult` and raw dictionary result forms. Includes parameterization-safety tests that pass a `DROP TABLE` payload as a value and assert it is never executed. Note that dictionary keys are bare column names - the library prepends the `@` itself |
| `SchemaEvolutionTests` | 29 | Sequential | Behaviors from `Docs/schema-evolution.md`: index and trigger changes, column drops, and the unsupported-change contract |
| `SharedConnectionTests` | 7 | Sequential | Shared connection locking, timeouts, multi-caller safety |
| `SupportedDataTypesStorageTests` | 50 | Sequential | Storage of every supported data type and its round-trip behavior |
| `SxmExceptionContractTests` | 9 | (default) | Public `SxmException` metadata contract: error codes, `Data["sxmErrorCode"]`, context, exception filters |
| `SxmTransactionBehaviorTests` | 30 | Sequential | Validates `Docs/advanced-transaction-processing.md`: commit/rollback on dispose, `IsFaulted`, your own exceptions vs. SQLiteXM failures, set-up failures before the SQL runs, cancelled reads and writes, failed LINQ update/delete and bulk insert, skipped writes returning 0, staged commits, nested and different-database transactions, uncommitted-work visibility and isolation, and use after disposal |
| `TransactionPatternTests` | 14 | Sequential | The five transaction patterns from `Docs/application-lifecycle.md`: mixed operations, fault behavior, fault recovery, multiple commits, all-operation-type conformance |
| `TransactionTests` | 7 | Sequential | Commit/rollback, atomicity, ambient transactions, nested-create rejection |
| `UpdateSetTests` | 10 | Sequential | `SxmUpdateSet<T>` edge cases behind `Set().UpdateAsync()`: same column set twice, null assignment, multiple distinct columns, no-match predicates, expression-valued setters, mixed value/expression chains, and builder immutability. Several are characterization tests that record current behavior rather than a pre-specified contract |

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
dotnet test --configuration Debug --framework net10.0
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
| Transactions | All documented patterns | Commit, rollback, ambient, fault + recovery, multi-commit, `IsFaulted`, cancellation, set-up failures, staged commits, nesting, isolation |
| Mixed unit of work | Combined operation types | Entity DML + SQL + LINQ sharing one transaction |
| Basic LINQ queries | Core operators | Where, OrderBy, Select, Count, First/Single |
| Advanced LINQ | Composite queries | GroupBy, joins, set operations, aggregates, paging, deferred execution |
| Documented LINQ patterns | Every documented example | Mirrors `Docs/linq-queries.md` one-to-one |
| Bulk insert | All three entry points | `SxmSql`, `SxmTransaction`, LINQ extension; batching, id/synchId population, rollback |
| Bulk update/delete | LINQ bulk paths | `Set().UpdateAsync()` / `DeleteAsync()`, predicates, transaction scoping, and `SxmUpdateSet<T>` edge cases (duplicate column assignment, null assignment, expression setters, builder immutability) |
| Schema migration | Supported changes + refusals | Column add, rename, drop; unsupported-change contracts; failed-migration retry |
| Table drop | Dependency rules | Dependency refusal, drop ordering, recreate |
| Connection management | Normal and contended paths | Worker leases, shared connections, lock contention, timeouts. Pool exhaustion not exercised |
| Fail-fast validation | Registration and construction | Early validation errors |
| Database options validation | Every rule in `SxmDatabaseOptionsValidator` | Errors vs. warnings, defaults accepted as valid, all errors reported together |
| Database options taking effect | PRAGMA round-trips and connection interceptors | `journal_mode`, `synchronous`, `foreign_keys`, `temp_store`, `cache_size`, `busy_timeout`, `wal_autocheckpoint`, combined-option application, and `OnConnectionOpened` / `OnConnectionClosed` invocation and ordering |
| Ad-hoc SQL execution | `RunStatementAsync` overload matrix | Inline SQL with no parameters, named (`Dictionary`) parameters and positional (`List`) parameters, typed and raw result shapes, SELECT/UPDATE/DELETE, and parameterization safety against injection payloads |
| Named SQL statements | Statement-name resolution from `statements.json` | `select` / `insert` / `update` / `delete` definitions executed by name, named vs. positional binding, name-before-SQL resolution order, and unknown / null / empty name failures |
| Exception contract | Public metadata surface | `SxmException` error codes, `Data["sxmErrorCode"]`, context, filters |
| Initialization pattern | Startup contract | `StartInitialization` + `EnsureReadyAsync`, idempotency, concurrency, failure propagation |
| Multi-database | Routing and isolation | Multiple databases, per-database transactions, LINQ, performance |
| Performance and scale | Indicative, mostly unthresholded | Bulk, large-dataset, concurrency, memory-leak checks. Timings are environment-dependent, not guarantees |

## What's NOT Tested (Future Coverage)

Each item below corresponds to a qualified entry in the scope table above.

- **Cascading deletes (`ON DELETE CASCADE`)** — the library supports `ForeignKeyDeleteAction.Cascade` and emits the clause, but no test sets it or verifies that deleting a parent row removes its children. Multi-level FK *chains* are covered by `DropTableTests` (`GrandParent -> Parent -> Child`); what is missing is row-level cascade behaviour. (`PRAGMA foreign_keys`, which cascades depend on, is now verified by `DatabaseOptionsEffectTests`.)
- **Trigger execution verification**
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
