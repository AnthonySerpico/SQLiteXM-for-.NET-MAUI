## Basic Database Initialization

This guide shows how to initialize the database from the normal UI launch path. 
This is when a user launches the application normally, and the normal MAUI startup path 
is executed.


> 💡 If you only ever access the database from the normal UI launch path, this guide is all you need. However, 
if your app has multiple startup entry points — headless background work, BroadcastReceivers, background services, 
etc. — and they need database access, you will, at some point, also need our [Advanced Initialization](./advanced-initialization-patterns.md) guide.
But that can wait till later.

Below is a basic database initialization wrapped in an `async` static method.

```csharp
    public static async Task StartDatabaseAsync()
    {
        // Create an array containing all the entities used by your application
        Type[] entityTypes = new Type[]
        {
            typeof(Customer),
            typeof(Post)
        };

        // Open the SqlStatements.json file from the application package
        Stream stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

        // Start database initialization in the background
        // SQLiteXM takes ownership of 'stream' and ensures proper disposal
        SxmDatabase.StartInitialization(stream, databaseOptions : null, entityTypes);
    }
```

Initialization can be performed anywhere in your app. All that is required is that you initialize the
database before using it. The question is, where should you call `StartDatabaseAsync()`?

One option is to call it from `MauiProgram.CreateMauiApp()`; like this: `_ = StartDatabaseAsync();`

`StartDatabaseAsync` can be called anywhere in `CreateMauiApp`. This gets database initialization started  early in the application lifecycle, before the UI is even created. `SxmDatabase.StartInitialization` 
returns immediately and runs in the background and does not block other startup work.

## Verifying Database Initialization Has Completed

After starting initialization, the next step before interacting with the database is to confirm that 
initialization has completed. This is done by calling:

```csharp
await SxmDatabase.EnsureReadyAsync();
```

`EnsureReadyAsync` will wait for initialization to complete if it is still in progress. If initialization has already 
completed, it returns immediately. It is not necessary to call `EnsureReadyAsync` immediately after calling 
`StartInitialization`. All that is required is that you call it at least once before accessing the database.

> ⚠️ If `StartInitialization` fails (for example, the SQL statements file can't be parsed, or the database can't be 
> created/opened), `EnsureReadyAsync` will throw the exception that caused the failure. Every call site shown below 
> should account for this.

All that's left is to decide where to make the call. The general guidance is to 
call `EnsureReadyAsync` as late as possible, just before you need to access the database. Let's assume you need the 
database very early in the application lifecycle. In this case, there are two good options depending on *how* early 
you need access.

One possibility is to call `EnsureReadyAsync` in the `OnStart` method of your `App` class.

```csharp
protected async override void OnStart()
{
    base.OnStart();

    // When EnsureReadyAsync returns, the database is ready for use.
    await SxmDatabase.EnsureReadyAsync();
}
```

What if you need access even earlier, for example, you need the database during AppShell construction.
In this case, you will need to initialize before `new AppShell()`. 

Add the `InitializeShellAfterDbReadyAsync` method below to your `App` class, then call it from `CreateWindow`; 
like this: `_ = InitializeShellAfterDbReadyAsync(window)`

```csharp
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new ContentPage
            {
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            });

            // Kick off DB initialization only now that the window exists, so the 
            // continuation below can never race ahead of window creation.
            _ = InitializeShellAfterDbReadyAsync(window);

            return window;
        }

        // NOTE: Minimal implementation - no error handling included.
        private async Task InitializeShellAfterDbReadyAsync(Window window)
        {
            // Wait for the database to be ready before initializing the shell.
            await SxmDatabase.EnsureReadyAsync();

            // Initialize the shell on the UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                window.Page = new AppShell();
            });
        }
```

## Summary

| Where to call it | What it looks like | When to use it |
|---|---|---|
| `MauiProgram.CreateMauiApp()` | `_ = StartDatabaseAsync();` | Almost always — this is where initialization should be *started*, as early as possible, before the UI is even built |
| `App.OnStart()` | `await SxmDatabase.EnsureReadyAsync();` | You don't need the database until the app has finished starting — e.g. before navigating to a page or loading data on-demand |
| `App.CreateWindow()` | `_ = InitializeShellAfterDbReadyAsync(window);` | You need database access while `AppShell` itself is being built, and can tolerate a brief loading screen while the database finishes initializing |
| Anywhere else in your app | `await SxmDatabase.EnsureReadyAsync();` | You'd rather wait until the moment you actually need the database — e.g. in a page, view model, or service — instead of gating startup on it |

---

