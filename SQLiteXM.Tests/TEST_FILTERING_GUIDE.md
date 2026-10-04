# SQLiteXM Test Filtering Guide

The suite currently discovers **386 tests per target framework** (`net8.0` and `net9.0`) across 26 test classes. Only one category trait is in use today:

- `[Trait("Category", "Performance")]` on `MultiDatabasePerformanceTests` (12 tests)

Everything else is untagged, so `Category!=Performance` is the practical "fast suite" filter.

> All commands must run in **Debug** configuration. The test project emits a build error for any other configuration, and a module initializer aborts the run if the assembly was compiled without `DEBUG`.

## Test Execution Options

### 1. Run ALL tests (both target frameworks)
```powershell
dotnet test --configuration Debug
```
Runs 384 tests on `net8.0` and again on `net9.0` (~768 executions), including the 10 performance tests.

### 2. Run everything EXCEPT performance tests
```powershell
dotnet test --configuration Debug --filter "Category!=Performance"
```
Use this for routine validation during development.

### 3. Run ONLY the performance tests
```powershell
dotnet test --configuration Debug --filter "Category=Performance"
```

### 4. Run a single target framework
```powershell
dotnet test --configuration Debug --framework net9.0
```
Halves the run time when you don't need cross-TFM coverage. Combine with any `--filter`.

### 5. Run one specific test
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~BulkInsert_10000Products"
```

### 6. List tests without running them
```powershell
dotnet test --configuration Debug -t
```

## Test Breakdown

### Performance tests (12 - `MultiDatabasePerformanceTests`)
- `BulkInsert_10000Products_CompletesInReasonableTime`
- `BulkInsert_AcrossMultipleDatabases_PerformsWell`
- `Query_LargeDataset_50KRecords_PerformsEfficiently`
- `Query_ComplexLinq_LargeDataset_PerformsWell`
- `Aggregates_LargeDataset_PerformEfficiently`
- `ConcurrentWrites_MultipleDatabases_100Operations_NoDeadlocks`
- `HighConcurrency_200SimultaneousOperations_HandlesGracefully`
- `MixedReadWrite_HighConcurrency_PerformsWell`
- `LongRunningOperations_1000Iterations_NoMemoryLeak`
- `UpdateOperations_LargeDataset_ApplyAllChanges` (correctness, no timing assertion)
- `UpdateOperations_TransactionBatching_OutperformsAutocommit` (asserts a speedup ratio, not a duration)
- `UpdateOperations_LargeDataset_ReportsThroughput` (benchmark, no pass/fail threshold)

### Benchmark test (1 - not trait-tagged)
`LargeSchemaInitializationBenchmarkTests.StartInitialization_WithLargeSchema_75TablesBy50Columns_ReportsElapsedTime` measures initialization over a 75-table x 50-column schema and writes elapsed time to test output. It has **no pass/fail threshold**, but it is slower than a typical unit test. Exclude it explicitly when you want the fastest possible loop:
```powershell
dotnet test --configuration Debug --filter "Category!=Performance&FullyQualifiedName!~LargeSchemaInitializationBenchmark"
```

### Functional tests (373)

Everything else: entity initialization and CRUD, null handling, schema migration and evolution, column rename, table drop, transactions and transaction patterns, mixed unit of work, LINQ (basic, advanced, documented patterns, bulk operations), bulk insert, connection management, exception contract, initialization pattern, and multi-database functional/LINQ tests.

Per-class counts are listed in [README.md](README.md).

## Filtering by Class or Area

### A single test class
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~SQLiteXM.Tests.NullHandlingTests"
```

### All multi-database tests (functional, LINQ, and performance)
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~MultiDatabase"
```

### Multi-database functional only
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~MultiDatabase&Category!=Performance"
```

### All transaction-related classes
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~Transaction"
```

### All LINQ-related classes
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~Linq"
```

### Schema-related classes
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~SchemaEvolution|FullyQualifiedName~Migration|FullyQualifiedName~ColumnRename|FullyQualifiedName~DropTable"
```

### A single theory case
`BulkInsertTests` and `NullHandlingTests` use `[Theory]` heavily, so a method filter runs every data case for that method:
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~BulkInsert_ShouldPopulateIds_MatchingDatabaseRows"
```

## Collection Behavior (why ordering matters)

| Collection | Classes | Parallel? |
|------------|---------|-----------|
| `Sequential` | Most classes | No |
| `MultiDatabase` | `MultiDatabaseTests`, `MultiDatabaseLinqTests`, `MultiDatabasePerformanceTests` | No |

All three multi-database classes share the `MultiDatabase` collection because they call `SxmDatabase.ResetForTestingAsync()`, which mutates process-wide state.
| `InitializationPattern` | `InitializationPatternTests`, `LargeSchemaInitializationBenchmarkTests` | No |

All three collections set `DisableParallelization = true` because tests share one database file and, in the case of `MultiDatabase` and `InitializationPattern`, reset process-wide SQLiteXM state. Filtering down to a subset is always safe; the collections restore standard `TestBase` configuration when they finish.

## Recommended Workflow

### During active development
```powershell
dotnet test --configuration Debug --framework net9.0 --filter "Category!=Performance"
```

### Before committing
```powershell
dotnet test --configuration Debug --filter "Category!=Performance"
```

### Before a release or PR merge
```powershell
dotnet test --configuration Debug
```

### When investigating performance
```powershell
dotnet test --configuration Debug --filter "Category=Performance"
```

## CI/CD Recommendations

### Pull request validation
- Run `Category!=Performance` across both target frameworks for fast feedback.

### Nightly builds
- Run the full suite including performance tests and record the benchmark output over time.

### Release validation
- Always run the full suite on both target frameworks and confirm no performance regressions.

## Visual Studio Test Explorer

- **All tests**: clear all filters
- **No performance**: trait filter `Category != Performance`
- **Only performance**: trait filter `Category = Performance`
- **By class**: group by Class and run the node you care about

## Adding New Trait Categories

Only `Category=Performance` exists today. If you add a new long-running or environment-dependent class, tag it at the class level so the existing filter conventions keep working:

```csharp
[Collection("Sequential")]
[Trait("Category", "Performance")]
public class MyExpensiveTests : TestBase { }
```

Then document the new trait here so the filter recipes stay accurate.
