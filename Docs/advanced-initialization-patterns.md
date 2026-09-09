# Advanced Database Initialization Patterns

On .NET MAUI, an application can have multiple entry points into application code, and their 
timing and ordering may be unpredictable.

For example:


- **Normal UI launch** — the user launches the application normally.
- **Deep links / app links / universal links** — the OS launches or activates the application in response to a URI or associated web link.
- **Android system callbacks** — Android invokes application code in response to broadcasts or other system events, such as `BroadcastReceiver`s.
- **Background execution** — Android services, OS-managed background tasks, and similar mechanisms execute application code without requiring a visible UI.
- **Push and location callbacks** — the OS invokes application code in response to push notifications, geofences, location events, or similar triggers.

*Every one of them* may need the database to be ready before doing any work.

This guide documents the pattern used by SQLiteXM for solving this problem: a single, process-wide, thread-safe “database ready” signal that any entry point can await, with a cheap, non-blocking fast path once initialization has completed.

> 💡 If you only ever access the database from
> the normal UI launch path, the simple
> guidance in [Getting Started](getting-started.md) is enough. Read this guide
> if your app has multiple startup entry points (headless background work,
> `BroadcastReceiver`s, background services, etc.).

---

## The Requirements

- Each of these entry points needs the ability to independently trigger database initialization when required.
- We cannot make any assumptions about the order or timing in which these entry points will be called.
- Database initialization must be safe to trigger from any entry point, at any time, concurrently, and must be guaranteed to execute only once per application process.
- There must be a mechanism that guarantees that database-dependent work cannot proceed until database initialization has successfully completed. If initialization fails, the failure must be propagated to callers rather than allowing them to proceed as though the database were ready.
- Initialization must not block the main thread, and must be able to run concurrently with other startup work.
- The database initialization completion check must be cheap and non-blocking once initialization has successfully completed.


## The Solution

The solution is a static, process-wide `TaskCompletionSource` that represents successful completion of database initialization, combined with a lock-protected, idempotent initialization trigger. The initialization trigger guarantees that initialization is performed only once per application process, while the `TaskCompletionSource` provides a single task that any entry point can await. On successful initialization, the task is completed with `TrySetResult()`. If initialization fails, the task is completed with `TrySetException(ex)`, allowing callers awaiting database readiness to observe the initialization failure.

---
## SQLiteXM Database Initialization

SQLiteXM's built-in database initialization is performed by calling `SxmDatabase.StartInitialization(...)`.
`StartInitialization` returns immediately without blocking. Initialization runs in the background and
implements *“The Solution”* listed above. Its usage is detailed in [Getting Started](./getting-started.md).

Below is a brief recap of the usage pattern:

```csharp
    // Create an array containing all the entities used by your application
    Type[] applicationEntities = new Type[]
    {
        typeof(User), 
        typeof(Order), 
        typeof(Product)
    };

    // Open the SqlStatements.json file from the application package
    Stream sqlStatementsStream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    // Start database initialization in the background
    // SQLiteXM takes ownership of 'sqlStatementsStream' and ensures proper disposal.
    SxmDatabase.StartInitialization(sqlStatementsStream, databaseOptions: null, applicationEntities);

    // Later, when database access is required:
    await SxmDatabase.EnsureReadyAsync();
```

Each entry point into your application that requires database access should call `StartInitialization`. 
You do not need to know whether another entry point has already started or completed 
initialization. SQLiteXM handles concurrent and repeated calls safely.

You can call `StartInitialization` from anywhere (including headless 
startup paths like Android `BroadcastReceiver`s or iOS background handlers). You do not need to synchronize 
calls to `StartInitialization` either - SQLiteXM handles this internally. And it is safe to call multiple 
times and concurrently; only the first call actually performs initialization. Subsequent calls wait until 
initialization completes or return immediately if initialization has already completed.

## Verifying Database Initialization Has Completed

Everywhere in your application where you call `StartInitialization`, you must also ensure 
initialization has completed before attempting to access the database. 

This is done by calling `EnsureReadyAsync`:

```csharp
await SxmDatabase.EnsureReadyAsync();
```

Each entry point only needs to call `EnsureReadyAsync` once before accessing the database.
It waits for the database initialization task started by `StartInitialization` 
to complete, guaranteeing that the database is fully initialized and ready for use. It's 
not required that you call `EnsureReadyAsync` immediately after calling `StartInitialization`. 
You can call it at any point later in your code, as long as it is called before accessing the database.

You decide *where* in your app it makes the most sense to await initialization.

`EnsureReadyAsync` is safe to call from UI-thread async code, view models, or background services alike 
and is safe to call multiple times and concurrently. Once initialization has completed, subsequent 
calls return immediately. 


---

## Headless Startup Example - Android `BroadcastReceiver`s & iOS Background Tasks

A application can be activated by the OS **without ever running your normal MAUI
UI startup path** (`CreateMauiApp()`/`App()`) — for example, an Android
`BroadcastReceiver` responding to a system event, or an iOS background
refresh/processing task. If that headless code needs the database, it must
independently ensure the database is initialized and ready for work.

The examples below show only what's needed to make the database usable
during headless startup — not the platform-specific mechanics of receiving
the broadcast or background task itself.

### Android: `BroadcastReceiver`

```csharp
public override async void OnReceive(Context? context, Intent? intent)
{
    // Required: without this, Android considers OnReceive "finished" as soon
    // as it hits the first 'await' below, and may kill the process before
    // database initialization completes.
    PendingResult pendingResult = GoAsync();

    try
    {
        // Create an array containing all the entities used by your application
        Type[] applicationEntities = new Type[]
        {
            typeof(User), 
            typeof(Order), 
            typeof(Product)
        };

        // Open the SqlStatements.json file from the application package
        Stream sqlStatementsStream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

        // Start database initialization in the background
        // SQLiteXM takes ownership of 'sqlStatementsStream' and ensures proper disposal.
        SxmDatabase.StartInitialization(sqlStatementsStream, databaseOptions: null, applicationEntities);
        
        // Wait for StartInitialization to finish processing
        await SxmDatabase.EnsureReadyAsync();

        // The database is now safe to use.
    }
    catch (Exception ex)
    {
        // Handle any exceptions that occurred during database initialization
        Console.WriteLine($"Database initialization failed: {ex}");
    }
    finally
    {
       pendingResult.Finish();
    }
}
```

> ⚠️ Because `OnReceive` is `async void`, Android considers it "finished" as
> soon as it hits the first `await` unless you tell it otherwise. Call
> `PendingResult pendingResult = GoAsync();` before the `await`, and
> `pendingResult.Finish();` once you're done, so the process isn't killed
> mid-initialization. This is unrelated to the database readiness pattern
> itself — it's a general requirement for any `async` work inside
> `OnReceive`.

Daisy-chained BroadcastReceivers are safe. If one BroadcastReceiver triggers another, each receiver 
can independently call `StartInitialization` and `EnsureReadyAsync`. SQLiteXM safely handles the 
initialization regardless of which receiver runs first or whether multiple receivers trigger 
initialization concurrently.


### iOS: Background App Refresh / Processing Task

The iOS equivalent of a headless wake-up is a background task (e.g., a
`BGAppRefreshTask`/`BGProcessingTask` handler, or the older
`PerformFetch` callback). The database readiness check is identical:

```csharp
async void HandleAppRefresh(BGAppRefreshTask task)
{
    // Create an array containing all the entities used by your application
    Type[] applicationEntities = new Type[]
    {
        typeof(User), 
        typeof(Order), 
        typeof(Product)
    };

    // Open the SqlStatements.json file from the application package
    Stream sqlStatementsStream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    // Start database initialization in the background
    // SQLiteXM takes ownership of 'sqlStatementsStream' and ensures proper disposal.
    SxmDatabase.StartInitialization(sqlStatementsStream, databaseOptions: null, applicationEntities);
        
    try
    {
        // Wait for StartInitialization to finish processing        
        await SxmDatabase.EnsureReadyAsync();

        // The database is now safe to use.
	
        task.SetTaskCompleted(true);
    }
    catch (Exception ex)
    {
        // Handle any exceptions that occurred during database initialization
        Console.WriteLine($"Database initialization failed: {ex}");

        task.SetTaskCompleted(false);
    }
}
```

> ⚠️ The platform-specific background-task registration, expiration handling, and completion requirements are outside the scope of this article. The example above focuses only on ensuring the database is initialized before the background task accesses it.

Multiple background tasks are safe. If multiple iOS background tasks require database access, each task 
can independently call `StartInitialization` and `EnsureReadyAsync`. SQLiteXM safely handles database 
initialization regardless of which task runs first or whether multiple tasks initialize
concurrently.

---

## Implementing Custom Initialization

If you want to write your own initialization code that bypasses SQLiteXM's built-in initialization, you can do so by directly accessing the following two lower-level database initialization methods and wrapping them in whatever initialization logic you need. 

- `SxmDatabase.InitializeAsync(Stream, SxmDatabaseOptions)`
- `SxmDatabase.RegisterEntitiesAsync(Type[])`

```csharp
    // Open the SqlStatements.json file from the application package
    using Stream sqlStatementsStream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
    // Initialize the database
    await SxmDatabase.InitializeAsync(sqlStatementsStream, databaseOptions: null);

    // Create an array containing all the entities used by your application
    Type[] applicationEntities = new Type[]
    {
        typeof(User), 
        typeof(Order), 
        typeof(Product)
    };
    // Register your application entities
    await SxmDatabase.RegisterEntitiesAsync(applicationEntities);
```

### What the Initialization Methods Do

Initialization and registration together perform several important tasks.

InitializeAsync():

- Reads the database configuration from `SqlStatements.json`
- Opens/creates the configured databases
- Applies the database options and PRAGMA settings
- Prepares SQLiteXM

RegisterEntitiesAsync():

- Tells SQLiteXM which entity types belong to the ORM
- Inspects entity metadata
- Creates/updates the corresponding database schema

### The Rules

1. **Order matters.** `SxmDatabase.InitializeAsync` must be called before `SxmDatabase.RegisterEntitiesAsync`.
2. `SxmDatabase.InitializeAsync` must finish processing before calling `SxmDatabase.RegisterEntitiesAsync`.
3. When `SxmDatabase.RegisterEntitiesAsync` finishes processing, the database is ready for use.
4. You can call these methods at any point in your app's processing cycle you choose.
5. `SxmDatabase.RegisterEntitiesAsync` does not need to be called immediately after `SxmDatabase.InitializeAsync`. It can be called anytime after.
6. If `SxmDatabase.InitializeAsync` throws an exception, do not attempt to run `SxmDatabase.RegisterEntitiesAsync`. Doing so may permanently corrupt the database schema.
7. Do not run any code that attempts to interact with the database until both methods have successfully completed.


### Database Options

The second parameter of `InitializeAsync()` is an optional `SxmDatabaseOptions` instance used to customize the operation of SQLiteXM and the SQLite database.

This is covered fully in: ➡️ **[Database Options](./database-configuration-options.md)**

---

## Test Coverage

We consider this initialization pattern to be a critical part of SQLiteXM's correctness and reliability. The
claims made in this guide are verified with an automated test suite plus a large-schema performance benchmark.

For the full breakdown — including which tests verify which claims, a notable concurrency finding, and measured
initialization times against a 75-table/3,750-column stress-test schema — see:

➡️ **[Advanced Initialization Patterns — Test Coverage](./advanced-initialization-patterns-test-coverage.md)**