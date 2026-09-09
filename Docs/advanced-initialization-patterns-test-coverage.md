# Advanced Initialization Patterns — Test Coverage

This document summarizes the automated test coverage for the initialization pattern described in
⬅️ **[Advanced Database Initialization Patterns](./advanced-initialization-patterns.md)**.

We consider this initialization pattern to be a critical part of SQLiteXM's correctness and reliability. And have decided to
expose the results of our internal test coverage. The following summarizes the claims made in that guide and the corresponding
automated tests that verify them. The same tests are available as part of the SQLiteXM.Tests project, and can be run to verify
the correctness of the implementation in your own environment.

The initialization pattern is validated by an automated test suite
(`SQLiteXM.Tests\InitializationPatternTests.cs`) that exercises `SxmDatabase.StartInitialization`
and `SxmDatabase.EnsureReadyAsync` directly, including high-concurrency scenarios, multi-entity
registration, and multiple distinct failure modes.

## What's Verified

| Documented claim | Verified by |
|---|---|
| Safe to call from any entry point, at any time, concurrently | `StartInitialization_CalledMultipleTimesConcurrently_*`, `StartInitialization_HighConcurrency_ManySimultaneousCallsResultInExactlyOneWinner` — 50 threads released simultaneously via a `ManualResetEventSlim` barrier, each calling `StartInitialization` with a distinct entity list; verifies exactly one wins regardless of scale |
| Guaranteed to execute only once per process | Same tests — asserts an XOR condition (`simpleEntityWorks ^ allTypesEntityWorks`) after the race, proving exactly one competing entity list is ever registered, never both and never neither |
| Initialization must not block the calling thread | `StartInitialization_ReturnsImmediatelyWithoutBlockingCallingThread` — wraps the call itself in a `Stopwatch` and asserts it returns in under 25ms, even while queuing 9 entity types (including FK/index/trigger schema work) for background registration; in practice the call consistently completes in well under 1ms (measured ~0.3–0.5ms on a warm CLR) — the 25ms bound only exists to absorb cold-JIT/slow-CI variance without masking a real regression |
| Failure must propagate to callers via `EnsureReadyAsync` | `StartInitialization_WithMalformedSqlStatementsFile_*` — a deliberately malformed JSON stream causes failure during the parse/`InitializeAsync` stage; `StartInitialization_WithAbstractEntityType_*` — an abstract entity type causes failure during the later `RegisterEntitiesAsync` stage; both cases confirm `EnsureReadyAsync()` faults with the originating exception |
| No retry after failure (still "only once") | `StartInitialization_CalledAgainAfterAFailure_DoesNotRetryAndRemainsFaulted` — after an initial call fails on a malformed file, a second independent call with a fully valid statements file and entity list is made; `EnsureReadyAsync()` still observes the *original* failure, proving there is no silent auto-retry |
| `EnsureReadyAsync` safe to call repeatedly/concurrently, cheap once ready | `EnsureReadyAsync_CalledConcurrentlyManyTimes_AllCompleteSuccessfully` — fires 20 concurrent `EnsureReadyAsync()` calls via `Task.WhenAll`, then awaits it once more afterward to confirm the fast, already-completed path also succeeds |
| Stream ownership/disposal contract | `StartInitialization_DisposesTheStreamOfBothTheWinningAndTheLosingCall` — asserts `ObjectDisposedException` on `ReadByte()` for *both* the winning and losing call's stream; `StartInitialization_CalledAgainAfterSuccess_IsNoOpAndStillDisposesTheUnusedStream` — confirms a stream passed to a late, no-op call is still disposed even though its contents are never read |
| Entities are actually usable end-to-end (not just "no exception") | `StartInitialization_ThenEnsureReadyAsync_CompletesAndEntityIsUsable` — a single entity is saved and re-queried via `SxmTransaction` after `EnsureReadyAsync()`; `StartInitialization_WithManyEntityTypesIncludingRelationsIndexesAndTriggers_AllRegisteredAndUsable` — registers 9 entity types in one call and exercises a foreign-key relationship, composite/unique/single-field indexes, an `AFTER UPDATE` trigger, and required-field columns, each verified with a real save + query |

## A Notable Finding

Testing at high concurrency (50 simultaneous callers) surfaced a subtlety worth calling out: because
`StartInitialization` dispatches its work to a background `Task.Run`, "the first call wins" means
whichever call's background task wins the internal lock race — **not necessarily the call that
appears first in source order** when multiple callers race concurrently. The library's behavior is
still fully correct and safe (exactly one call's entities are ever registered, and every caller
observes the same successful result via `EnsureReadyAsync`), but application code must not assume a
specific call "wins" when entry points race — only that exactly one of them will.

## Large-Schema Performance Benchmark

Beyond correctness, we also benchmark the initialization pattern against a deliberately extreme
schema to characterize how `StartInitialization`/`EnsureReadyAsync` scale with table and column
count. This is not a pass/fail test — it exists to give a concrete, measured sense of
worst-case cold-start cost versus everyday "schema already exists" cost, run via
`SQLiteXM.Tests\LargeSchemaInitializationBenchmarkTests.cs`.

| Schema dimension | Value |
|---|---|
| Entities (tables) | 75 |
| Columns per entity | 50 (3,750 columns total) |
| Single-column `[Index]` attributes | 600 (8 per entity) |
| Composite `[Index(nameof(...), nameof(...))]` attributes | 25 (every 3rd entity) |
| `[UniqueIndex(nameof(...))]` attributes | 18 (every 4th entity) |
| `[RequiredNotNull]` attributes | 15 (every 9th and every 11th entity) |
| Total index-creation statements | 643 |
| Total schema-relevant attributes | 733 |

| Scenario | Measured elapsed time |
|---|---|
| First run — `StartInitialization` + `EnsureReadyAsync` building the schema from scratch | ~1.4 seconds |
| Second run — same process, gating state cleared, schema already exists (simulated migration/relaunch) | ~90 milliseconds |

Even for a schema this large — far beyond what a typical mobile app is expected to need — cold
schema creation completes in about a second and a half, and every subsequent "database already
exists" pass (the common case for app relaunches) completes in well under 100ms. Because
`StartInitialization` runs this work on a background task, none of this blocks the calling thread
regardless of schema size.

## Scope and Limitations

These tests validate the library-level contract (`SxmDatabase`'s actual implementation) with full
confidence. The platform-specific integration examples in the main guide — the Android
`BroadcastReceiver` (`GoAsync()`/`PendingResult.Finish()`) and iOS background task
(`BGAppRefreshTask`/`SetTaskCompleted`) patterns — are correct as written but depend on OS process
lifecycle behavior that cannot be exercised by unit tests. Those patterns are validated by code
review against platform documentation rather than automated tests, and should be confirmed with
device/emulator testing when adopted in a real application.

---

⬅️ Back to **[Advanced Database Initialization Patterns](./advanced-initialization-patterns.md)**
