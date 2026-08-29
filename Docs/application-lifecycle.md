# Application Lifecycle Integration

SQLiteXM provides **optional** lifecycle hooks to manage database operations when your MAUI application is suspended or resumed by the operating system. These hooks provide an additional layer of protection during app backgrounding, particularly useful for mobile applications.

> 💡 **This feature is completely optional.** SQLiteXM works perfectly fine without lifecycle integration. SQLite's built-in Write-Ahead Logging (WAL) and atomic commits already provide crash resistance. Lifecycle hooks add a best-effort cleanup layer for apps that want extra protection during suspension.

---

## Should You Use Lifecycle Hooks?

### Mobile Apps (iOS & Android) — ⭐ Recommended

Mobile operating systems aggressively manage app lifecycle:
- Apps are suspended when backgrounded
- The OS may terminate apps without warning to reclaim memory
- Operations can be interrupted mid-transaction

**Lifecycle hooks are most valuable on mobile**, where suspension/termination is frequent and unpredictable.

### Desktop/Windows Apps — 🤷 Optional

Windows apps face less aggressive suspension:
- Apps typically remain running when minimized
- Termination is less frequent
- Users have more control over app lifecycle

**Lifecycle hooks provide minimal benefit on Windows**, but can still be used for consistency across platforms.

### When to Use Lifecycle Hooks

✅ **Use lifecycle hooks if:**
- You're building a mobile-first MAUI app
- You want defense-in-depth protection during backgrounding
- You want to minimize risk of interrupted writes during suspension
- You're okay with blocking new operations while the app is backgrounded

❌ **Skip lifecycle hooks if:**
- You're building a desktop-only Windows app
- You prefer simpler code without lifecycle management
- You're comfortable relying on SQLite's built-in crash recovery
- Blocking operations during backgrounding would harm your app's behavior

---

## How Lifecycle Hooks Work

SQLiteXM's lifecycle manager provides **best-effort cleanup** during app suspension:

1. **Blocks new database connections** immediately when the app is suspended
2. **Waits for a grace period (default: 5 seconds)**, giving in-flight database operations an opportunity to complete
3. **Resumes normal operation** when the app is foregrounded

> ⚠️ **Important**: This is not a guarantee. The OS can still terminate your app mid-operation, but this feature reduces the likelihood by using the available suspension time productively.

> 💡 **Defense in Depth**: SQLite's WAL already handles crashes gracefully. Lifecycle hooks add an extra layer by attempting orderly cleanup before the OS terminates the app.

---

## Quick Start (Mobile Apps)

For iOS and Android MAUI apps, hook SQLiteXM lifecycle events in your `App.xaml.cs`:

```csharp
using SQLiteXM;

namespace MauiApp1
{
	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();

			MainPage = new AppShell();
		}

		protected override void OnSleep()
		{	
			SxmLifecycleManager.OnSleep();
		}

		protected override void OnResume()
		{
			SxmLifecycleManager.OnResume();
		}
	}
}
```

> 💡 **That's it!** These two optional lines provide best-effort cleanup during app backgrounding.

> 💡 **Call SxmLifecycleManager.OnSleep() last.** OnSleep() is synchronous and may block the calling thread for up to SuspendGracePeriod while existing database operations are given an opportunity to complete.

> 💡 **For Windows-only apps**, you can omit these hooks entirely. SQLite's built-in crash recovery is typically sufficient for desktop scenarios.

---

## How It Works

### OnSleep Flow

When the operating system suspends your application (user switches apps, locks device, etc.):

```text
User backgrounds app
		↓
OnSleep() triggered
		↓
SxmLifecycleManager.OnSleep() called
		↓
┌─────────────────────────────────────────┐
│ 1. Block new connection creation        │
│    (sets lifecycle gate = closed)       │
└─────────────────────────────────────────┘
		↓
┌─────────────────────────────────────────┐
│ 2. Wait for grace period (default: 5s)  │
│    (allows in-flight operations to      │
│     complete - best effort)             │
└─────────────────────────────────────────┘
		↓
SQLiteXM remains in backgrounded state
```

**During the grace period:**
- ✅ Existing operations complete normally (best effort)
- ❌ New database operations throw `SxmException` with error code `ConnectionBlockedBackgrounded`

**After the grace period:**
- OnSleep() returns. SQLiteXM remains in its backgrounded state.
- The OS may terminate the app at any time (SQLite's WAL handles crash recovery)

### OnResume Flow

When the user returns to your application:

```text
User foregrounds app
		↓
OnResume() triggered
		↓
SxmLifecycleManager.OnResume() called
		↓
┌─────────────────────────────────────────┐
│ 1. Allow new connection creation        │
│    (sets lifecycle gate = open)         │
└─────────────────────────────────────────┘
		↓
Normal operation resumes
```

---

## API Reference

### OnSleep

Signals that the application is being suspended.

**Signature:**
```csharp
public static void OnSleep()
```

**Behavior:**
1. Blocks new database connections immediately
2. Waits for the configured grace period to allow in-flight operations to complete (best effort)
3. Returns after the grace period expires

**Usage:**
```csharp
protected override void OnSleep()
{
	SxmLifecycleManager.OnSleep();
}
```

> 💡 **Optional Feature**: You can omit this hook entirely if you prefer to rely on SQLite's built-in crash recovery.

**Remarks:**
- Safe to call multiple times (idempotent)
- If already suspended, subsequent calls are no-ops
- The grace period starts immediately; don't delay calling this method

---

### OnResume

Signals that the application has returned to the foreground.

**Signature:**
```csharp
public static void OnResume()
```

**Behavior:**
1. Re-enable new database connections immediately
2. Returns synchronously

**Usage:**
```csharp
protected override void OnResume()
{
	SxmLifecycleManager.OnResume();
}
```

**Remarks:**
- Safe to call multiple times (idempotent)
- If not suspended, calls are no-ops
- Synchronous method; safe to call from `OnResume()` override

---

### SuspendGracePeriod

Configures how long `OnSleep()` waits before considering the app safe to suspend.

**Signature:**
```csharp
public static TimeSpan SuspendGracePeriod { get; set; }
```

**Default Value:**
- `TimeSpan.FromSeconds(5)` (5 seconds)

**Behavior:**
- Allows in-flight database operations to complete before full suspension
- Longer periods are safer but delay suspension
- Shorter periods reduce suspension delay but may not give operations enough time

**Exceptions:**
- `ArgumentOutOfRangeException` — Thrown if set to a negative value

**Usage:**
```csharp
// Set grace period to 3 seconds (faster suspension, less cleanup time)
SxmLifecycleManager.SuspendGracePeriod = TimeSpan.FromSeconds(3);

// Set grace period to 10 seconds (more cleanup time, slower suspension)
SxmLifecycleManager.SuspendGracePeriod = TimeSpan.FromSeconds(10);
```

**Recommendations:**

| Grace Period | Use Case |
|---|---|
| **1-3 seconds** | Fast operations only; quicker app backgrounding |
| **5 seconds (default)** | General-purpose mobile apps; balances cleanup time and responsiveness |
| **Custom** | Applications with a demonstrated need for a different trade-off |

> 💡 **Tuning the grace period**: The default 5 seconds is a pragmatic balance for most mobile apps. Monitor your app's database operation patterns. If operations are frequently interrupted during suspension, increase the grace period. If app backgrounding feels sluggish, decrease it.

> 💡 Avoid unnecessarily large grace periods. OnSleep() is synchronous and blocks the calling thread while the grace period elapses.

---

## What Happens When Operations Are Blocked

When the lifecycle gate is closed (app is suspended), new database connection attempts throw `SxmException`:

```csharp
protected override void OnSleep()
{
	SxmLifecycleManager.OnSleep();
}

// Later, user backgrounds the app...

try
{
	// This will fail if called after OnSleep blocks connections
	var customer = new Customer { Name = "Alice" };
	await customer.SaveAsync(); // throws SxmException
}
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded)
{
	// Handle gracefully: queue for retry, log, or notify user
	Console.WriteLine("Operation blocked: app is backgrounded");
}
```

**Error Details:**
- **Error Code**: `SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded`
- **Error Message**: `"Cannot create connection for '{database}': application is backgrounded."`

> 💡 **SQLite's Safety Net**: Even if an operation is interrupted, SQLite's Write-Ahead Logging (WAL) ensures the database remains in a consistent state. Lifecycle hooks reduce the likelihood of interruption, but they don't eliminate it — and that's okay, because SQLite already handles crashes gracefully.

---

## Advanced Scenarios


### Adjusting Grace Period Based on Operation Type

If your app has predictable operation patterns, adjust the grace period dynamically:

```csharp
public async Task PerformLargeExportAsync()
{
	// Temporarily extend grace period for long operation
	var originalGracePeriod = SxmLifecycleManager.SuspendGracePeriod;
	SxmLifecycleManager.SuspendGracePeriod = TimeSpan.FromSeconds(8);

	try
	{
		// Perform large export
		await ExportAllDataAsync();
	}
	finally
	{
		// Restore original grace period
		SxmLifecycleManager.SuspendGracePeriod = originalGracePeriod;
	}
}
```

---


## Best Practices

### 1. Use Lifecycle Hooks for Mobile Apps

For iOS and Android MAUI apps, implement lifecycle hooks to take advantage of best-effort cleanup:

```csharp
protected override void OnSleep()
{
	SxmLifecycleManager.OnSleep(); // ✅ Recommended for mobile
}

protected override void OnResume()
{
	SxmLifecycleManager.OnResume(); // ✅ Recommended for mobile
}
```

For Windows-only apps, lifecycle hooks are optional and provide minimal benefit.

### 2. Must Hook Both Events (If You Use Lifecycle Management)

If you implement `OnSleep`, you must also implement `OnResume`:

```csharp
protected override void OnSleep()
{
	SxmLifecycleManager.OnSleep();
}

protected override void OnResume()
{
	SxmLifecycleManager.OnResume();
}
```

> ❌ **Don't**: Implement only `OnSleep` without `OnResume`. Both are required for proper state management.

### 3. Call Lifecycle Hooks At the Correct Time

Call OnResume() lifecycle hook first and OnSleep() lifecycle hook last:

```csharp
protected override void OnSleep()
{
	// Then handle other suspension logic
	SaveAppState();

	// ✅ Call lifecycle hook LAST
	SxmLifecycleManager.OnSleep();
}

protected override void OnResume()
{
	// ✅ Call lifecycle hook FIRST
	SxmLifecycleManager.OnResume();

	// Then handle other resume logic
	RestoreAppState();
}
```

### 4. Handle Blocked Operations Gracefully

Catch `SxmException` with `ConnectionBlockedBackgrounded` and handle appropriately:

```csharp
try
{
	await customer.SaveAsync();
}
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded)
{
	// Queue for retry on resume, or notify user
	QueueForRetry(() => customer.SaveAsync());
}
```

### 5. Test Lifecycle Transitions

Test your app's behavior during lifecycle transitions:
- Background the app mid-operation
- Force-kill the app while backgrounded
- Resume after various durations
- Test on real devices, not just emulators

### 6. Understand the Limitations

Lifecycle hooks provide **best-effort** cleanup, not guarantees:
- The OS can still terminate your app mid-operation
- Race conditions are still possible between gate checks and operation execution
- SQLite's WAL already handles crashes; lifecycle hooks add an extra layer but aren't required

If your app works fine without lifecycle hooks, that's normal — SQLite is already crash-resistant.

### 7. Don't Start Operations in OnSleep

Never start database operations in `OnSleep()` — new connections are blocked:

```csharp
protected override void OnSleep()
{
	SxmLifecycleManager.OnSleep();

	// ❌ WRONG: This will throw SxmException
	await SaveCurrentStateAsync(); // uses database
}
```

If you need to save state, do it **before** `OnSleep` is called, or queue it for `OnResume`.

---

## Frequently Asked Questions

### Do I need lifecycle hooks for my app?

**For mobile apps (iOS/Android)**: Recommended but optional. They provide best-effort cleanup during backgrounding.

**For Windows apps**: Not necessary. SQLite's built-in crash recovery is sufficient for desktop scenarios.

### What if I don't use lifecycle hooks?

Your app will work fine. SQLite's Write-Ahead Logging (WAL) already handles crashes gracefully. Lifecycle hooks add an extra layer of protection but aren't required.

### Can the OS still kill my app mid-operation?

Yes. Lifecycle hooks reduce the likelihood by using the grace period for cleanup, but they can't prevent all interruptions. SQLite's WAL ensures the database remains consistent even if the app is terminated unexpectedly.

### Why not make this automatic?

Hooking `OnSleep`/`OnResume` requires access to the `Application` class, which is app-specific. We provide the hooks as optional API surface so developers can integrate them if desired.

### Does this affect performance?

No. Lifecycle hooks only activate during app suspension/resume, which are infrequent events. There's no performance impact during normal operation.

---

## Troubleshooting

### "Cannot create connection: application is backgrounded"

**Problem**: You see `SxmException` with `ConnectionBlockedBackgrounded` error code.

**Cause**: You attempted to create a database connection after `OnSleep()` was called.

**Solution**:
- Catch the exception and queue the operation for retry on resume
- Avoid starting database operations in response to background events
- Check if operations are being triggered inappropriately during suspension

---

### Operations Don't Complete Before Suspension

**Problem**: Database operations are interrupted when the app is suspended.

**Cause**: The grace period is too short for your operations to complete, or the OS is terminating the app aggressively.

**Solution**:
- Increase `SuspendGracePeriod`:
  ```csharp
  SxmLifecycleManager.SuspendGracePeriod = TimeSpan.FromSeconds(10);
  ```
- Optimize or break apart long-running operations
- Remember: This is best-effort. SQLite's WAL will recover even if operations are interrupted.

---

### App Feels Sluggish When Backgrounding

**Problem**: There's a noticeable delay when the user backgrounds the app.

**Cause**: The grace period is too long, delaying suspension.

**Solution**:
- Reduce `SuspendGracePeriod`:
  ```csharp
  SxmLifecycleManager.SuspendGracePeriod = TimeSpan.FromSeconds(2);
  ```
- Ensure you're not running long operations synchronously in `OnSleep`
- Profile your app to identify delays outside SQLiteXM

---

### OnResume Not Restoring Connections

**Problem**: Database operations fail even after `OnResume()` is called.

**Cause**: `OnResume()` is not being called, or there's a different error.

**Solution**:
- Verify you've hooked `OnResume()` in `App.xaml.cs`
- Check the exception details — it may not be a lifecycle issue
- Test on a real device; emulator lifecycle behavior can differ

