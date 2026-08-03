# DirectBindingDemo - Modern Direct Entity Binding Pattern

## Overview

DirectBindingDemo showcases the **modern direct entity binding pattern** enabled by SQLiteXM's `SxmEntity` base class. This sample demonstrates how UI controls can bind directly to entity properties, eliminating the need for intermediate ViewModel properties and manual data synchronization.

## What This Sample Demonstrates

### Core Patterns

- **Direct Entity Binding**: UI binds directly to `SxmEntity` properties via `INotifyPropertyChanged`
- **Computed Properties**: Entity-level computed properties that automatically update the UI
- **Zero Property Copying**: No manual mapping between ViewModel and Entity layers
- **Transaction Pattern**: Saving multiple related entities atomically
- **When NOT to Direct Bind**: Security examples (passwords) showing when to use ViewModel properties

### Key Features

1. **Multi-Step Registration Flow**
   - Step 1: Email & Password (demonstrates mixed binding approach)
   - Step 2: Personal Information (shows computed properties: FullName, Age)
   - Step 3: Preferences (demonstrates binding to related entities)
   - Step 4: Review & Complete (shows transaction-based saves)

2. **Educational Comments**
   - Inline explanations of the direct binding pattern
   - Highlights when computed properties update automatically
   - Shows security considerations for sensitive data

3. **Real-Time Computed Properties**
   - `FullName` updates as you type FirstName/LastName
   - `Age` updates when DateOfBirth changes
   - No ViewModel orchestration required

## Project Structure

```
DirectBindingDemo/
├── Models/
│   ├── User.cs                  # User entity with computed properties
│   └── UserPreferences.cs       # User preferences entity
├── ViewModels/
│   ├── BaseViewModel.cs         # Base ViewModel with common properties
│   ├── EmailPasswordViewModel.cs
│   ├── PersonalInfoViewModel.cs
│   ├── PreferencesViewModel.cs
│   └── ReviewViewModel.cs
├── Views/
│   ├── WelcomePage.xaml        # Entry point
│   ├── EmailPasswordPage.xaml
│   ├── PersonalInfoPage.xaml
│   ├── PreferencesPage.xaml
│   ├── ReviewPage.xaml
│   └── HomePage.xaml           # Post-registration home
├── Services/
│   └── PasswordHasher.cs       # Password hashing utility
└── Resources/
	└── Raw/
		└── SqlStatements.json  # Database definition (AppData)
```

## Architecture Highlights

### Direct Entity Binding Pattern

```csharp
// ViewModel exposes the entity directly
public partial class PersonalInfoViewModel : BaseViewModel
{
	[ObservableProperty]
	private User currentUser = new User();

	private async Task NextAsync()
	{
		// No manual mapping! Entity already has latest values from UI
		await CurrentUser.SaveAsync();
	}
}
```

**XAML Binds Directly to Entity Properties:**
```xml
<Entry Text="{Binding CurrentUser.FirstName}" />
<Entry Text="{Binding CurrentUser.LastName}" />
<Label Text="{Binding CurrentUser.FullName}" />  <!-- Computed property! -->
```

### Computed Properties in Entities

```csharp
[Table(Database = "AppData")]
public class User : SxmEntity
{
	private string? _firstName;
	private string? _lastName;

	public string? FirstName
	{
		get => _firstName;
		set
		{
			if (SetProperty(ref _firstName, value))
			{
				// Notify UI that FullName also changed
				OnPropertyChanged(nameof(FullName));
			}
		}
	}

	public string? LastName
	{
		get => _lastName;
		set
		{
			if (SetProperty(ref _lastName, value))
			{
				OnPropertyChanged(nameof(FullName));
			}
		}
	}

	// Computed property - no database column
	[IgnoreColumn]
	public string FullName => $"{FirstName} {LastName}".Trim();
}
```

### When to Use This Pattern

✅ **Use DirectBindingDemo pattern when:**
- You want minimal boilerplate code
- Your entities have computed properties that should reflect in the UI
- You don't need transformation layers between UI and database
- You want the entity to be the single source of truth
- Your business logic can live in the entity itself

❌ **Don't use direct binding for:**
- Passwords or sensitive data requiring transformation
- Fields requiring complex validation before assignment
- Temporary UI state that shouldn't persist

## Database Configuration

The `SqlStatements.json` file defines a single database:

```json
{
  "databases": [
	{
	  "database": "AppData",
	  "isDefault": true,
	  "version": 1
	}
  ]
}
```

All entities (`User` and `UserPreferences`) use the `AppData` database:

```csharp
[Table(Database = "AppData", IsColumnAttributeRequired = false)]
public class User : SxmEntity { }

[Table(Database = "AppData", IsColumnAttributeRequired = false)]
public class UserPreferences : SxmEntity { }
```

## Key Code Examples

### Loading an Entity for Direct Binding

```csharp
private async Task LoadUserAsync()
{
	await using (var context = new SxmDbContext("AppData"))
	var user = context.GetTable<User>()
		.FirstOrDefault(u => u.id == UserId);

	if (user != null)
	{
		// Set entity as CurrentUser
		// UI bindings now read/write directly to this entity
		CurrentUser = user;
	}
}
```

### Saving with Transactions

```csharp
// Save related entities atomically
var connection = new SxmConnection("AppData", shared: false);
await using var transaction = await SxmSqlTransaction.CreateAsync(connection);
{
	try
	{
		// Entity already has latest values from UI bindings
		await CurrentUser.SaveAsync(transaction);
		await CurrentPreferences.SaveAsync(transaction);
		await transaction.CommitTransactionAsync();
	}
	catch
	{
		// Transaction rolls back automatically on error
		throw;
	}
}
```

### Mixed Binding Approach (Security Example)

```csharp
// Email: Direct binding to entity
public User CurrentUser { get; set; } = new User();

// Password: ViewModel property for validation/hashing
[ObservableProperty]
private string password = string.Empty;

private async Task NextAsync()
{
	// Hash password before storing in entity
	CurrentUser.PasswordHash = PasswordHasher.HashPassword(Password);
	await CurrentUser.SaveAsync();
}
```

```xml
<!-- Direct binding -->
<Entry Text="{Binding CurrentUser.Email}" />

<!-- ViewModel property for security -->
<Entry Text="{Binding Password}" IsPassword="True" />
```

## Benefits of Direct Binding

### 1. Less Code
**Traditional MVVM:**
```csharp
// ViewModel properties
[ObservableProperty] private string firstName;
[ObservableProperty] private string lastName;
[ObservableProperty] private string email;

// Manual mapping
user.FirstName = FirstName;
user.LastName = LastName;
user.Email = Email;
```

**Direct Binding:**
```csharp
// Just expose the entity
[ObservableProperty] private User currentUser;

// No mapping needed - entity already updated by UI!
```

### 2. Computed Properties Work Automatically
```xml
<!-- Type in FirstName/LastName fields -->
<Entry Text="{Binding CurrentUser.FirstName}" />
<Entry Text="{Binding CurrentUser.LastName}" />

<!-- FullName updates automatically! -->
<Label Text="{Binding CurrentUser.FullName}" />
```

### 3. Single Source of Truth
- No synchronization bugs between ViewModel and Entity
- Entity properties contain the current, accurate values
- Business logic lives where it belongs (in the entity)

## Running the Sample

1. **Open the solution** in Visual Studio 2022 or later
2. **Set DirectBindingDemo as startup project**
3. **Select your target platform** (Windows, Android, iOS, or macOS)
4. **Run the application**
5. **Watch the computed properties** update as you type (Step 2: Personal Information)
6. **Review the educational comments** in the XAML pages

## Educational Highlights

### Step 1: Email & Password
- Shows **when to use direct binding** (Email field)
- Shows **when NOT to use direct binding** (Password fields - security)
- Demonstrates **mixed approach** for real-world scenarios

### Step 2: Personal Information
- Demonstrates **FullName** computed property updating as you type
- Demonstrates **Age** computed property updating when date changes
- Educational comments explain the significance of zero ViewModel logic

### Step 3: Preferences
- Shows direct binding to **related entities** (`UserPreferences`)
- Demonstrates working with multiple entities in one ViewModel

### Step 4: Review & Complete
- Shows **transaction pattern** for atomic saves
- Demonstrates binding to entity properties for display

## Comparing with RegistrationDemo

| Feature | RegistrationDemo | DirectBindingDemo |
|---------|------------------|-------------------|
| Binding Target | ViewModel properties | Entity properties directly |
| Property Mapping | Manual (ViewModel → Entity) | Automatic (UI ↔ Entity) |
| Computed Properties | In ViewModel | In Entity |
| Code Complexity | More boilerplate | Less boilerplate |
| Business Logic Location | ViewModel | Entity |
| Best For | Validation/transformation layers | Simple CRUD with computed properties |
| Lines of Code | More | Fewer |

## Learn More

- See **RegistrationDemo** for the traditional MVVM pattern with intermediate properties
- Read the [INotifyPropertyChanged Support Documentation](../../docs/INotifyPropertyChanged-Support.md) for details on how SQLiteXM enables direct binding
- Explore [SQLiteXM Documentation](../../docs/) for LINQ queries, transactions, and more

## Technologies Used

- **.NET MAUI** - Cross-platform UI framework
- **SQLiteXM** - SQLite ORM with `INotifyPropertyChanged` support in entities
- **CommunityToolkit.Mvvm** - MVVM helpers and source generators
- **SQLite** - Local database engine

## Key Takeaway

Direct entity binding is **not just a shortcut** - it's a architectural pattern that:
- Reduces code by 30-50% compared to traditional MVVM
- Eliminates entire classes of synchronization bugs
- Enables entity-level computed properties and business logic
- Makes the entity the single source of truth

Use this pattern when your entities are smart enough to validate and compute their own state!
