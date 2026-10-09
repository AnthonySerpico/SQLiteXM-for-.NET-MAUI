# SQLiteXM Test Filtering Guide

The suite currently discovers **529 tests per target framework** (`net8.0` and `net9.0`, so 1,058 executions in a full run) across 31 test classes. Only one category trait is in use today:

- `[Trait("Category", "Performance")]` on `MultiDatabasePerformanceTests` (12 tests)

Everything else is untagged, so `Category!=Performance` is the practical "fast suite" filter.

> Both **Debug** and **Release** configurations are supported. The old Debug-only build gate and its runtime guard have been removed, and the test-support reset hooks now compile into both configurations. The examples below use Debug because it is the usual development loop; substitute `--configuration Release` wherever you need it.

## Test Execution Options

### 1. Run ALL tests (both target frameworks)
```powershell
dotnet test --configuration Debug
```
Runs 529 tests on `net8.0` and again on `net9.0` (1,058 executions), including the 12 performance tests.

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

### Functional tests (516)

That is 529 minus the 12 trait-tagged performance tests and the 1 untagged benchmark. If you filter only on `Category!=Performance`, you get 517 because the benchmark is not trait-tagged.

Functional coverage spans: entity initialization and CRUD, null handling, schema migration and evolution, column rename, table drop, transactions and transaction patterns, mixed unit of work, LINQ (basic, advanced, documented patterns, bulk operations), bulk insert, connection management, exception contract, initialization pattern, database options (validation and runtime PRAGMA effects), raw SQL `RunStatementAsync` and named statements, `SxmUpdateSet` edge cases, and multi-database functional/LINQ tests.

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

### Database options (validation and runtime effects)
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~DatabaseOptions"
```

### Raw SQL and named statements
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~RunStatementTests|FullyQualifiedName~NamedStatementTests"
```

### A single theory case
`BulkInsertTests`, `NullHandlingTests` and `DatabaseOptionsValidatorTests` use `[Theory]` heavily
```powershell
dotnet test --configuration Debug --filter "FullyQualifiedName~BulkInsert_ShouldPopulateIds_MatchingDatabaseRows"
```

## Collection Behavior (why ordering matters)

Parallelization is disabled **assembly-wide** by `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `AssemblyInfo.cs`. Collections therefore group classes by the shared state they mutate rather than by scheduling need.

| Collection | Classes | Parallel? |
|------------|---------|-----------|
| `Sequential` | Most classes | No |
| `MultiDatabase` | `MultiDatabaseTests`, `MultiDatabaseLinqTests`, `MultiDatabasePerformanceTests` | No |
| `InitializationPattern` | `InitializationPatternTests`, `LargeSchemaInitializationBenchmarkTests` | No |
| `DatabaseOptionsEffect` | `DatabaseOptionsEffectTests` | No |
| `NamedStatement` | `NamedStatementTests` | No |

The four non-`Sequential` collections exist because their classes call `SxmDatabase.ResetForTestingAsync()` and reinitialize SQLiteXM with their own configuration, which mutates process-wide state. `DatabaseOptionsEffectTests` reinitializes with per-test `SxmDatabaseOptions`; `NamedStatementTests` reinitializes with its own generated statements file.

Filtering down to a subset is always safe: each of these classes restores the standard `TestBase` configuration when it finishes.

`SxmExceptionContractTests` declares no collection and runs in the default one. It asserts only on exception metadata and does not touch shared database state.

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
dotnet test --configuration Release
```

### When investigating performance
```powershell
dotnet test --configuration Debug --filter "Category=Performance"
```

Note that Release runs under JIT optimization, so timing figures from `MultiDatabasePerformanceTests` are not comparable across configurations.

## CI/CD Recommendations

### Pull request validation
- Run `Category!=Performance` across both target frameworks for fast feedback.

### Nightly builds
- Run the full suite including performance tests and record the benchmark output over time.

### Release validation
- Always run the full suite on both target frameworks, in both Debug and Release, and confirm no performance regressions.

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

If your new class resets or reinitializes SQLiteXM, give it its own collection definition with `DisableParallelization = true` and restore the standard `TestBase` configuration in `Dispose`, following `NamedStatementTests` as the pattern. Then run the full suite to confirm it does not leak state into the other classes.
