# INotifyPropertyChanged Support in SQLiteXM

SQLiteXM provides built-in support for `INotifyPropertyChanged` in the `SxmEntity` base class, enabling seamless two-way data binding between your entities and .NET MAUI UI controls—with zero boilerplate code.

## Overview

All entities that inherit from `SxmEntity` automatically implement `INotifyPropertyChanged`, allowing you to:
- ✅ Bind entity properties directly to MAUI UI controls
- ✅ Get automatic UI updates when entity properties change
- ✅ Eliminate repetitive property notification code
- ✅ Use entities directly in MVVM patterns without wrapper ViewModels

## Quick Start

### Basic Entity with Data Binding

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	private string? _name;
	public string? Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
	}

	private string? _email;
	public string? Email
	{
		get => _email;
		set => SetProperty(ref _email, value);
	}

	private int _age;
	public int Age
	{
		get => _age;
		set => SetProperty(ref _age, value);
	}
}
```

### XAML Binding

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
			 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
			 x:Class="MyApp.CustomerPage">
	<StackLayout Padding="20">
		<Entry Text="{Binding Customer.Name}" 
			   Placeholder="Customer Name" />

		<Entry Text="{Binding Customer.Email}" 
			   Placeholder="Email Address" />

		<Stepper Value="{Binding Customer.Age}" 
				 Minimum="0" 
				 Maximum="120" />

		<Label Text="{Binding Customer.Age, StringFormat='Age: {0}'}" />

		<Button Text="Save" 
				Command="{Binding SaveCommand}" />
	</StackLayout>
</ContentPage>
```

### Code-Behind

```csharp
public partial class CustomerPage : ContentPage
{
	public Customer Customer { get; set; }

	public CustomerPage()
	{
		InitializeComponent();

		Customer = new Customer 
		{ 
			Name = "John Doe",
			Email = "john@example.com",
			Age = 30
		};

		BindingContext = this;
	}

	private async void OnSaveClicked(object sender, EventArgs e)
	{
		await Customer.SaveAsync();
		await DisplayAlert("Success", "Customer saved!", "OK");
	}
}
```

When the user types in the `Entry` controls or adjusts the `Stepper`, the `Customer` entity properties update automatically. When you change properties in code, the UI updates automatically.

## API Reference

### SetProperty Method

The `SetProperty` helper method is the recommended way to implement property setters for bindable properties.

#### Basic Overload

```csharp
protected bool SetProperty<T>(
	ref T storage, 
	T value, 
	[CallerMemberName] string? propertyName = null)
```

**Parameters:**
- `storage` - Reference to the backing field
- `value` - New value to assign
- `propertyName` - Property name (automatically provided by compiler)

**Returns:** `true` if the value changed, `false` if it was already equal

**Example:**
```csharp
private string? _firstName;
public string? FirstName
{
	get => _firstName;
	set => SetProperty(ref _firstName, value);
}
```

#### Callback Overload

```csharp
protected bool SetProperty<T>(
	ref T storage, 
	T value, 
	Action onChanged,
	[CallerMemberName] string? propertyName = null)
```

Use this overload when you need to trigger additional logic after a property changes.

**Example with Dependent Properties:**
```csharp
[Table(IsColumnAttributeRequired = false)]
public class Person : SxmEntity
{
	private string? _firstName;
	public string? FirstName
	{
		get => _firstName;
		set => SetProperty(ref _firstName, value, () => 
			OnPropertyChanged(nameof(FullName)));
	}

	private string? _lastName;
	public string? LastName
	{
		get => _lastName;
		set => SetProperty(ref _lastName, value, () => 
			OnPropertyChanged(nameof(FullName)));
	}

	// Computed property - notified when FirstName or LastName changes
	public string FullName => $"{FirstName} {LastName}".Trim();
}
```

### OnPropertyChanged Method

Manually raise the `PropertyChanged` event. Useful for computed properties or custom notification scenarios.

```csharp
protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
```

**Example:**
```csharp
public string DisplayName => $"{FirstName} {LastName}";

private void UpdateName(string first, string last)
{
	_firstName = first;
	_lastName = last;
	OnPropertyChanged(nameof(FirstName));
	OnPropertyChanged(nameof(LastName));
	OnPropertyChanged(nameof(DisplayName));
}
```

## Advanced Scenarios

### Mixed Property Types

Not all properties need to use `SetProperty`. Simple properties that don't require UI binding can remain as auto-properties:

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	// Bindable property for UI
	private decimal _total;
	public decimal Total
	{
		get => _total;
		set => SetProperty(ref _total, value);
	}

	// Simple property - no UI binding needed
	[ForeignKey(foreignTable: "Customer")]
	public long CustomerId { get; set; }

	// Timestamp - no UI binding
	public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
```

### Validation and Side Effects

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Product : SxmEntity
{
	private decimal _price;
	public decimal Price
	{
		get => _price;
		set => SetProperty(ref _price, value, () =>
		{
			// Validate
			if (_price < 0)
				_price = 0;

			// Update dependent properties
			OnPropertyChanged(nameof(PriceWithTax));
			OnPropertyChanged(nameof(DisplayPrice));
		});
	}

	public decimal TaxRate { get; set; } = 0.08m;

	public decimal PriceWithTax => Price * (1 + TaxRate);

	public string DisplayPrice => $"${Price:F2}";
}
```

### Collection Notifications

For observable collections, use `ObservableCollection<T>`:

```csharp
using System.Collections.ObjectModel;

[Table(IsColumnAttributeRequired = false)]
public class ShoppingCart : SxmEntity
{
	private ObservableCollection<CartItem> _items = new();

	[NotColumn]  // Don't persist the collection directly
	public ObservableCollection<CartItem> Items
	{
		get => _items;
		set => SetProperty(ref _items, value);
	}

	private decimal _total;
	public decimal Total
	{
		get => _total;
		set => SetProperty(ref _total, value);
	}

	public void RecalculateTotal()
	{
		Total = Items.Sum(item => item.Price * item.Quantity);
	}
}
```

## Using with MVVM Frameworks

### CommunityToolkit.Mvvm

SQLiteXM entities work seamlessly with CommunityToolkit.Mvvm. Use entities directly in your ViewModels:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class CustomerViewModel : ObservableObject
{
	// Entity property - manually implemented
	// Customer already handles its own property notifications
	private Customer? _customer;
	public Customer? Customer
	{
		get => _customer;
		set => SetProperty(ref _customer, value);
	}

	// ViewModel-only properties - use [ObservableProperty]
	[ObservableProperty]
	private bool _isSaving;

	[ObservableProperty]
	private string? _statusMessage;

	[RelayCommand]
	private async Task LoadCustomerAsync(long id)
	{
		IsSaving = true;
		try
		{
			using var ctx = await SxmTransaction.CreateAsync();
			Customer = await ctx.GetTable<Customer>()
				.FirstOrDefaultAsync(c => c.id == id);

			StatusMessage = "Customer loaded";
		}
		finally
		{
			IsSaving = false;
		}
	}

	[RelayCommand]
	private async Task SaveAsync()
	{
		if (Customer == null) return;

		IsSaving = true;
		try
		{
			await Customer.SaveAsync();
			StatusMessage = "Saved successfully";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error: {ex.Message}";
		}
		finally
		{
			IsSaving = false;
		}
	}
}
```

**Important:** Do NOT use `[ObservableObject]` attribute on `SxmEntity`-derived classes, as they already implement `INotifyPropertyChanged`.

```csharp
// ❌ DON'T DO THIS - Causes compiler error
[ObservableObject]
public partial class Customer : SxmEntity { }

// ✅ DO THIS - Use SxmEntity's built-in support
public class Customer : SxmEntity { }
```

### Prism, ReactiveUI, and Other Frameworks

The standard `INotifyPropertyChanged` implementation works with all MVVM frameworks:

```csharp
// Prism
public class CustomerViewModel : BindableBase
{
	private Customer? _customer;
	public Customer? Customer
	{
		get => _customer;
		set => SetProperty(ref _customer, value);
	}
}

// ReactiveUI
public class CustomerViewModel : ReactiveObject
{
	private Customer? _customer;
	public Customer? Customer
	{
		get => _customer;
		set => this.RaiseAndSetIfChanged(ref _customer, value);
	}
}
```

## Performance Considerations

### Change Detection

`SetProperty` uses `EqualityComparer<T>.Default.Equals()` to check if the value actually changed:

```csharp
// No event fired if value is the same
customer.Name = "John";
customer.Name = "John";  // No PropertyChanged event - value unchanged
```

This prevents unnecessary UI updates when the same value is assigned multiple times.

### Minimal Overhead

The `INotifyPropertyChanged` implementation adds minimal overhead:
- Event subscription/invocation: ~nanoseconds per property change
- Memory: One event handler delegate per subscribed property
- No impact on database operations or query performance

### When to Skip SetProperty

For write-only or internal properties that never bind to UI, you can skip `SetProperty`:

```csharp
[Table(IsColumnAttributeRequired = false)]
public class AuditLog : SxmEntity
{
	// These don't need UI binding
	public string? Action { get; set; }
	public DateTime Timestamp { get; set; }
	public string? UserId { get; set; }
}
```

## Common Patterns

### Master-Detail Binding

```csharp
public partial class OrderViewModel : ObservableObject
{
	private Order? _selectedOrder;
	public Order? SelectedOrder
	{
		get => _selectedOrder;
		set
		{
			if (SetProperty(ref _selectedOrder, value))
			{
				// Load order details when selection changes
				LoadOrderDetails();
			}
		}
	}

	[ObservableProperty]
	private ObservableCollection<OrderItem> _orderItems = new();

	private async void LoadOrderDetails()
	{
		if (SelectedOrder == null) return;

		using var ctx = await SxmTransaction.CreateAsync();
		var items = await ctx.GetTable<OrderItem>()
			.Where(i => i.OrderId == SelectedOrder.id)
			.ToListAsync();

		OrderItems = new ObservableCollection<OrderItem>(items);
	}
}
```

### Search and Filter

```csharp
public partial class CustomerListViewModel : ObservableObject
{
	[ObservableProperty]
	private string? _searchText;

	[ObservableProperty]
	private ObservableCollection<Customer> _customers = new();

	partial void OnSearchTextChanged(string? value)
	{
		_ = SearchCustomersAsync();
	}

	private async Task SearchCustomersAsync()
	{
		using var ctx = await SxmTransaction.CreateAsync();

		var query = ctx.GetTable<Customer>().AsQueryable();

		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			query = query.Where(c => 
				c.Name!.Contains(SearchText) || 
				c.Email!.Contains(SearchText));
		}

		var results = await query.ToListAsync();
		Customers = new ObservableCollection<Customer>(results);
	}
}
```

### Real-Time Updates

```csharp
[Table(IsColumnAttributeRequired = false)]
public class SensorReading : SxmEntity
{
	private double _temperature;
	public double Temperature
	{
		get => _temperature;
		set => SetProperty(ref _temperature, value, UpdateAlerts);
	}

	private bool _isAlertActive;
	public bool IsAlertActive
	{
		get => _isAlertActive;
		set => SetProperty(ref _isAlertActive, value);
	}

	private void UpdateAlerts()
	{
		IsAlertActive = Temperature > 100 || Temperature < 0;
	}
}

// In your XAML
<Label Text="{Binding Temperature, StringFormat='Temp: {0:F1}°C'}" />
<Label Text="ALERT!" 
	   IsVisible="{Binding IsAlertActive}"
	   TextColor="Red" />
```

## Troubleshooting

### UI Not Updating

**Problem:** Changes to entity properties don't update the UI.

**Solution:** Ensure you're using `SetProperty` in the property setter:

```csharp
// ❌ Wrong - no notification
public string? Name { get; set; }

// ✅ Correct - uses SetProperty
private string? _name;
public string? Name
{
	get => _name;
	set => SetProperty(ref _name, value);
}
```

### Binding Context Issues

**Problem:** Binding doesn't work at all.

**Solution:** Verify the `BindingContext` is set correctly:

```csharp
// In code-behind
public CustomerPage()
{
	InitializeComponent();
	BindingContext = this;  // Or your ViewModel
}

// In XAML
<ContentPage ...
			 x:DataType="local:CustomerViewModel">
```

### Computed Property Not Updating

**Problem:** A computed property doesn't refresh when dependencies change.

**Solution:** Manually call `OnPropertyChanged` for the computed property:

```csharp
private string? _firstName;
public string? FirstName
{
	get => _firstName;
	set => SetProperty(ref _firstName, value, () => 
		OnPropertyChanged(nameof(FullName)));  // Notify dependent property
}

public string FullName => $"{FirstName} {LastName}";
```

## Best Practices

1. **Use `SetProperty` for UI-bound properties** - Any property that binds to XAML should use `SetProperty`
2. **Keep auto-properties for simple data** - Foreign keys, timestamps, and internal state can remain auto-properties
3. **Minimize notifications in loops** - Batch updates and notify once if possible
4. **Use computed properties wisely** - Remember to notify dependent properties when their inputs change
5. **Leverage the callback overload** - Use `SetProperty` with a callback for validation and side effects

## Migration Guide

### From Manual INotifyPropertyChanged

If you previously implemented `INotifyPropertyChanged` manually:

**Before:**
```csharp
public class Customer : SxmEntity, INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private string? _name;
	public string? Name
	{
		get => _name;
		set
		{
			if (_name != value)
			{
				_name = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
			}
		}
	}
}
```

**After:**
```csharp
public class Customer : SxmEntity  // Already implements INotifyPropertyChanged
{
	private string? _name;
	public string? Name
	{
		get => _name;
		set => SetProperty(ref _name, value);  // Much simpler!
	}
}
```

### From ViewModel Wrappers

If you previously wrapped entities in ViewModels just for binding:

**Before:**
```csharp
public class CustomerViewModel : INotifyPropertyChanged
{
	private readonly Customer _customer;

	public string? Name
	{
		get => _customer.Name;
		set
		{
			_customer.Name = value;
			OnPropertyChanged();
		}
	}
	// ... repeat for every property
}
```

**After:**
```csharp
// Just use the entity directly!
public Customer Customer { get; set; }

// Bind in XAML
<Entry Text="{Binding Customer.Name}" />
```

## Additional Resources

- [.NET MAUI Data Binding](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/)
- [INotifyPropertyChanged Interface](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifypropertychanged)
- [CommunityToolkit.Mvvm Documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [SQLiteXM Documentation](../README.md)

## Summary

SQLiteXM's built-in `INotifyPropertyChanged` support eliminates the need for repetitive property notification code and enables seamless data binding in .NET MAUI applications. By inheriting from `SxmEntity`, your entities automatically gain:

- ✅ Two-way data binding support
- ✅ `SetProperty` helper methods
- ✅ Change detection and notification
- ✅ Compatibility with all MVVM frameworks

This feature makes SQLiteXM the ideal ORM choice for .NET MAUI + SQLite applications, allowing you to focus on building great apps instead of writing boilerplate code.
