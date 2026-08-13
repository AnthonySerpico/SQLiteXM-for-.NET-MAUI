# RegistrationDemo - Traditional MVVM Pattern

## Overview

RegistrationDemo showcases the **traditional MVVM pattern** for .NET MAUI applications using SQLiteXM. This sample demonstrates the conventional approach where ViewModels act as intermediaries between the UI and data entities, with explicit property mapping and data transformation.

## What This Sample Demonstrates

### Core Patterns

- **Traditional MVVM Architecture**: ViewModels expose properties that the UI binds to, then manually copy data to/from entities
- **Multi-Database Usage**: Demonstrates using multiple databases for different purposes
- **Draft/Resume Pattern**: Allows users to save progress and resume registration later
- **Data Validation & Transformation**: Shows validation logic and password hashing before saving to entities
- **LINQ Queries**: Examples of querying data using SQLiteXM's LINQ support

### Key Features

1. **Multi-Step Registration Flow**
   - Step 1: Email & Password
   - Step 2: Personal Information
   - Step 3: Preferences & Terms
   - Step 4: Review & Complete

2. **Two-Database Architecture**
   - **Session Database**: Stores `RegistrationDraft` entities for work-in-progress
   - **UserData Database**: Stores final `User` and `UserPreferences` entities

3. **Resume Capability**
   - Automatically saves progress at each step
   - Users can close the app and resume where they left off
   - Draft data is stored in a separate Session database

## Project Structure

```
RegistrationDemo/
├── Models/
│   ├── RegistrationDraft.cs    # Draft entity (Session database)
│   ├── User.cs                  # Final user entity (UserData database)
│   └── UserPreferences.cs       # User preferences entity (UserData database)
├── ViewModels/
│   ├── BaseViewModel.cs         # Base ViewModel with common properties
│   ├── EmailPasswordViewModel.cs
│   ├── PersonalInfoViewModel.cs
│   ├── PreferencesViewModel.cs
│   └── ReviewViewModel.cs
├── Views/
│   ├── WelcomePage.xaml        # Entry point with "New" or "Resume" options
│   ├── EmailPasswordPage.xaml
│   ├── PersonalInfoPage.xaml
│   ├── PreferencesPage.xaml
│   ├── ReviewPage.xaml
│   └── HomePage.xaml           # Post-registration home
├── Services/
│   └── PasswordHasher.cs       # Password hashing utility
└── Resources/
	└── Raw/
		└── SqlStatements.json  # Database definitions
```

## Architecture Highlights

### Traditional ViewModel Pattern

```csharp
// ViewModel exposes properties for UI binding
public partial class EmailPasswordViewModel : BaseViewModel
{
	[ObservableProperty]
	private string email = string.Empty;

	[ObservableProperty]
	private string password = string.Empty;

	private async Task NextAsync()
	{
		// Manual data mapping: ViewModel → Entity
		RegistrationDraft draft = await GetOrCreateDraftAsync();
		draft.Email = Email.Trim().ToLower();
		draft.PasswordHash = PasswordHasher.HashPassword(Password);
		await draft.SaveAsync();
	}
}
```

**XAML Binds to ViewModel Properties:**
```xml
<Entry Text="{Binding Email}" />
<Entry Text="{Binding Password}" IsPassword="True" />
```

### When to Use This Pattern

✅ **Use RegistrationDemo pattern when:**
- You need validation or transformation logic before saving
- You want clear separation between UI state and persisted data
- You're working with sensitive data (passwords, credit cards) that shouldn't be stored directly
- Your team is comfortable with traditional MVVM patterns
- You need to aggregate data from multiple sources before saving

## Database Configuration

The `SqlStatements.json` file defines two databases:

```json
{
  "databases": [
	{
	  "database": "Session",
	  "isDefault": false,
	  "version": 1
	},
	{
	  "database": "UserData",
	  "isDefault": true,
	  "version": 1
	}
  ]
}
```

### Session Database
- Stores temporary `RegistrationDraft` entities
- Allows resume functionality
- Can be cleared after successful registration

### UserData Database
- Stores permanent `User` and `UserPreferences` entities
- Contains the final validated and processed data

## Key Code Examples

### Creating a Draft (Session Database)

```csharp
using var sessionContext = new SxmTransaction("Session");
var existingDraft = sessionContext.GetTable<RegistrationDraft>()
	.FirstOrDefault(d => d.Email == email);

if (existingDraft == null)
{
	var draft = new RegistrationDraft
	{
		Email = email,
		CompletedStep = 1,
		StartedAt = DateTime.UtcNow
	};
	await draft.SaveAsync();
}
```

### Finalizing Registration (UserData Database)

```csharp
// Create final User entity
var user = new User
{
	Email = draft.Email,
	PasswordHash = draft.PasswordHash,
	FirstName = draft.FirstName,
	LastName = draft.LastName,
	DateOfBirth = draft.DateOfBirth,
	CreatedAt = DateTime.UtcNow
};

// Save with transaction
using var connection = new SxmConnection("UserData", shared: false);
await using var transaction = await SxmSqlTransaction.CreateAsync(connection);
{
	await user.SaveAsync(transaction);
	await userPreferences.SaveAsync(transaction);
	await transaction.CommitTransactionAsync();
}
```

## Running the Sample

1. **Open the solution** in Visual Studio 2022 or later
2. **Set RegistrationDemo as startup project**
3. **Select your target platform** (Windows, Android, iOS, or macOS)
4. **Run the application**
5. **Complete the registration flow** or test the resume functionality

## Comparing with DirectBindingDemo

| Feature | RegistrationDemo | DirectBindingDemo |
|---------|------------------|-------------------|
| Binding Target | ViewModel properties | Entity properties directly |
| Property Mapping | Manual (ViewModel → Entity) | Automatic (UI ↔ Entity) |
| Computed Properties | In ViewModel | In Entity |
| Code Complexity | More boilerplate | Less boilerplate |
| Best For | Validation/transformation layers | Simple CRUD with computed properties |
| Pattern | Traditional MVVM | Modern direct binding |

## Learn More

- See **DirectBindingDemo** for the modern direct entity binding pattern
- Read the [SQLiteXM Documentation](../../docs/) for more details on LINQ queries, transactions, and multi-database support

## Technologies Used

- **.NET MAUI** - Cross-platform UI framework
- **SQLiteXM** - SQLite ORM with LINQ support
- **CommunityToolkit.Mvvm** - MVVM helpers and source generators
- **SQLite** - Local database engine
