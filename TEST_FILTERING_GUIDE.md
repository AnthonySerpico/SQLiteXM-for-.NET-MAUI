# SQLiteXM Test Filtering Guide

## Performance Test Trait

The performance tests in `MultiDatabasePerformanceTests` are now tagged with `[Trait("Category", "Performance")]` to allow flexible test execution.

## Test Execution Options

### 1. Run ALL Tests (Including Performance - ~10-11 minutes)
```bash
dotnet test --configuration Debug
```

This will run all 181+ tests including the 10 performance tests.

### 2. Run Tests EXCLUDING Performance (~45 seconds)
```bash
dotnet test --filter "Category!=Performance" --configuration Debug
```

This runs all functional tests but skips the long-running performance tests. **Use this for quick validation during development.**

### 3. Run ONLY Performance Tests (~9-10 minutes)
```bash
dotnet test --filter "Category=Performance" --configuration Debug
```

This runs only the 10 performance/scale validation tests.

### 4. Run Specific Performance Test
```bash
dotnet test --filter "FullyQualifiedName~BulkInsert_10000Products" --configuration Debug
```

## Test Breakdown

### Functional Tests (171 tests - ~45 seconds)
- Core entity operations
- LINQ query tests
- Schema migration tests
- Multi-database CRUD and isolation tests (29 tests)
- Transaction tests
- Type mapping tests
- All other unit and integration tests

### Performance Tests (10 tests - ~9-10 minutes)
- `BulkInsert_10000Products_CompletesInReasonableTime`
- `BulkInsert_AcrossMultipleDatabases_PerformsWell`
- `Query_LargeDataset_50KRecords_PerformsEfficiently`
- `Query_ComplexLinq_LargeDataset_PerformsWell`
- `Aggregates_LargeDataset_PerformEfficiently`
- `ConcurrentWrites_MultipleDatabases_100Operations_NoDeadlocks`
- `HighConcurrency_200SimultaneousOperations_HandlesGracefully`
- `MixedReadWrite_HighConcurrency_PerformsWell`
- `LongRunningOperations_1000Iterations_NoMemoryLeak`
- `UpdateOperations_LargeDataset_PerformEfficiently`

## Recommended Workflow

### During Active Development
```bash
# Quick validation after code changes
dotnet test --filter "Category!=Performance"
```

### Before Committing
```bash
# Run all functional tests including multi-database
dotnet test --filter "Category!=Performance"
```

### Before Major Releases or PR Merges
```bash
# Run full suite including performance
dotnet test
```

### When Investigating Performance Issues
```bash
# Run only performance tests
dotnet test --filter "Category=Performance"
```

## CI/CD Recommendations

### Pull Request Validation
- Run functional tests only (fast feedback)
- Optionally run performance tests on schedule or for specific branches

### Nightly Builds
- Run full test suite including performance
- Track performance metrics over time

### Release Validation
- Always run full test suite including performance
- Validate no performance regressions

## Visual Studio Test Explorer

In Visual Studio, you can use the Test Explorer filter:
- **All tests**: Clear all filters
- **No performance**: Add trait filter `Category != Performance`
- **Only performance**: Add trait filter `Category = Performance`

## Additional Filtering Examples

### Run only multi-database tests (functional)
```bash
dotnet test --filter "FullyQualifiedName~MultiDatabase" --configuration Debug
```

### Run multi-database functional AND performance tests
```bash
dotnet test --filter "FullyQualifiedName~MultiDatabase" --configuration Debug
```
(This includes both `MultiDatabaseTests`, `MultiDatabaseLinqTests`, and `MultiDatabasePerformanceTests`)

### Exclude both performance AND slow integration tests
```bash
dotnet test --filter "Category!=Performance&Category!=Slow" --configuration Debug
```

## Summary

**Fast Development Cycle**: Use `--filter "Category!=Performance"` for quick (<1 min) validation.

**Comprehensive Validation**: Run all tests periodically to ensure performance characteristics remain acceptable.

The trait system gives you the flexibility to balance speed and thoroughness based on your needs.
