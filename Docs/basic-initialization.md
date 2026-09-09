## Basic Database Initialization

This guide shows options for initializing the database from the normal UI launch path. 
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
        Type[] entityTypes = new Type[]
        {
            typeof(Customer),
            typeof(Post)
        };

        Stream stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
        SxmDatabase.StartInitialization(stream, databaseOptions : null, entityTypes);
    }
```

Initialization can be performed anywhere in your app. All that is required is that you initialize the
database before using it. The question is, where should you call `StartDatabaseAsync()`?

One option is to call it from `MauiProgram.CreateMauiApp()`; like this: `_ = StartDatabaseAsync();`

`StartDatabaseAsync` can be called anywhere in `CreateMauiApp`. This gets database initialization started  early in the application lifecycle, before the UI is even created. `SxmDatabase.StartInitialization` 
returns immediately and runs in the background and does not block other startup work.


After starting initialization, the next step before interacting with the database is to confirm that 
initialization has completed. This is done by calling:

```csharp
await SxmDatabase.EnsureReadyAsync();
```

`EnsureReadyAsync` will wait for initialization to complete if it is still in progress. If initialization has already 
completed, it returns immediately. It is not necessary to call `EnsureReadyAsync` immediately after calling 
`StartInitialization`. All that is required is that you call it at least once before accessing the database.

All that's left is to decide where to make the call. The general guidance is to 
call it as late as possible, just before you need to access the database. Let's assume you need the database very early in the application lifecycle. 
In this case, there are two 
good options depending on *how* early you need access.

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

Add the method below to your `App` class, then call it from the `App` constructor
after `InitializeComponent()`; like this: `_ = InitializeShellAfterDbReadyAsync()`

```csharp
private async Task InitializeShellAfterDbReadyAsync()
{
    // 'MainPage' MUST be assigned synchronously here. MAUI calls CreateWindow() right after
    // this constructor returns. Activating a window with an unassigned MainPage is not a supported 
    // state in MAUI. Show a lightweight loading page here, then swap it out in the AppShell.
    MainPage = new ContentPage
    {
        Content = new ActivityIndicator
        {
            IsRunning = true,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }
    };

    // Wait for the database to be ready before initializing the shell
    await SxmDatabase.EnsureReadyAsync();

    // Initialize the shell on the UI thread
    await MainThread.InvokeOnMainThreadAsync(() => MainPage = new AppShell());
}
```

## Summary

| Entry point | When it runs | Use when... |
|---|---|---|
| `MauiProgram.CreateMauiApp()` - (`_ = StartDatabaseAsync();`) | Before the UI is created | Always. Unless you have a good reason to do otherwise — this is where initialization should be *started* |
| `App.OnStart()` - (`await EnsureReadyAsync();`) | After the app's `Window` is created and activated | You don't need the database until the app has finished starting, e.g. before navigating to a page or loading data on-demand |
| `App` constructor, via `InitializeShellAfterDbReadyAsync()` | Before `AppShell` is constructed | You need database access while building `AppShell` itself, and can tolerate a brief loading page while the database finishes initializing |
| Custom — anywhere else in your app | Wherever you choose | These are suggestions, not requirements. Both `StartDatabaseAsync()` and `EnsureReadyAsync` can be called from any page, view model, or service, as long as initialization is started and `EnsureReadyAsync` is awaited at least once before the database is accessed |

---

