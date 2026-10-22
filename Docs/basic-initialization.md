## Basic Database Initialization

This guide covers standard database initialization — this is when a user manually opens your app and the default MAUI startup lifecycle executes.

## Application Startup on Mobile

Startup on iOS and Android is time-critical and watchdog-enforced. Both platforms kill apps that block the main thread too long 
at launch. Synchronous initialization isn't merely slow, it's a crash risk. So initialization must overlap with app startup.

For this reason, SQLiteXM implements a two-step asynchronous pattern for initialization:

| Call | What it does |
|---|---|
| `StartInitialization` | Returns immediately. Database initialization is started in the background, overlapping with other startup work |
| `EnsureReadyAsync` | An awaitable "is database ready?" signal — waits if initialization is still in progress, returns instantly if already finished |

> 📌 **Note:** If your app only starts by a user opening it, this guide is all you need. 
However, if your app handles background entry points that can wake it automatically — like `BroadcastReceivers` 
or background services — you will eventually need our [Advanced Initialization](./advanced-initialization-patterns.md) 
guide. But that can wait till later.

---

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

        // Open the SqlStatements.json file located in the 
        // `Resources/Raw` folder of the application package.
        Stream stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

        // Start database initialization in the background. SQLiteXM 
        // takes ownership of 'stream' and ensures proper disposal
        SxmDatabase.StartInitialization(stream, databaseOptions : null, entityTypes);
    }
```

Initialization can be performed anywhere in your app. All that is required is that you initialize the
database before using it. The question is, where should you call `StartDatabaseAsync()`?

One option is to call it from `MauiProgram.CreateMauiApp()`, like this: `_ = StartDatabaseAsync();`. This gets database initialization started 
early in the application lifecycle. It's safe to call from anywhere in `CreateMauiApp`. 

## Verifying Database Initialization Has Completed

After starting initialization, the next step is to confirm that initialization has completed. This is done by calling:

```csharp
await SxmDatabase.EnsureReadyAsync();
```

`EnsureReadyAsync` will wait for initialization to complete if it is still in progress. If initialization has already 
completed, it returns immediately. It is not necessary to call `EnsureReadyAsync` immediately after calling 
`StartInitialization`. All that is required is that you call it at least once before accessing the database.

Where should you make this call? The general guidance is to call
`EnsureReadyAsync` as late as possible, just before you need to access the database. Let's assume you need the 
database very early in the application lifecycle. In this case, there are two good options depending on *how* 
early you need access.

One possibility is to call `EnsureReadyAsync` in the `OnStart` method of your `App` class.

```csharp
protected async override void OnStart()
{
    base.OnStart();

    // When EnsureReadyAsync returns, the database is ready for use.
    await SxmDatabase.EnsureReadyAsync();
}
```

> ⚠️ If `StartInitialization` fails (for example, the SQL statements file can't be parsed, or the database can't be 
> created/opened), `EnsureReadyAsync` will throw the exception that caused the failure. You should wrap your call to 
`EnsureReadyAsync` in a try/catch block and handle exceptions appropriately.

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

            // Start waiting for the database in the background.
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

## Trimming Warning IL2026

When you build for Release on Android or iOS, .NET trims unused code from your app. You may see:

```
IL2026: Using member 'SQLiteXM.SxmDatabase.StartInitialization(...)' which has 'RequiresUnreferencedCode'
may break when trimming.
```

**Your app is not broken.** SQLiteXM annotates the `SxmEntity` base class so that the trimmer preserves the
public properties and constructors of every entity that derives from it. Your columns are safe. The warning 
appears because `StartInitialization` receives entities as a `Type[]`, and the trimmer cannot statically prove 
what the elements of that array are. It warns conservatively.

---

## Summary

| Where to call it | What it looks like | When to use it |
|---|---|---|
| `MauiProgram.CreateMauiApp()` | `_ = StartDatabaseAsync();` | Almost always — this is where initialization should be *started*, as early as possible, before the UI is even built |
| `App.OnStart()` | `await SxmDatabase.EnsureReadyAsync();` | You don't need the database until the app has finished starting — e.g. before navigating to a page or loading data on-demand |
| `App.CreateWindow()` | `_ = InitializeShellAfterDbReadyAsync(window);` | You need database access while `AppShell` itself is being built, and can tolerate a brief loading screen while the database finishes initializing |
| Anywhere else in your app | `await SxmDatabase.EnsureReadyAsync();` | You'd rather wait until the moment you actually need the database — e.g. in a page, view model, or service — instead of gating startup on it |

---

