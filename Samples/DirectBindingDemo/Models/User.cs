using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace DirectBindingDemo.Models;

/// <summary>
/// User entity demonstrating DIRECT BINDING to SxmEntity.
/// 
/// KEY PATTERN DIFFERENCE from RegistrationDemo:
/// ================================================
/// - RegistrationDemo: UI binds to ViewModel properties → copied to/from entities
/// - DirectBindingDemo: UI binds DIRECTLY to User entity properties
/// 
/// This is possible because SxmEntity implements INotifyPropertyChanged and provides
/// the SetProperty() helper method. All mutable properties use SetProperty() in their
/// setters to automatically notify the UI when values change.
/// 
/// BINDING EXAMPLE:
/// ================
/// XAML: &lt;Entry Text="{Binding CurrentUser.FirstName}" /&gt;
/// 
/// When the user types, the change flows directly to User.FirstName.
/// When FirstName changes, the UI automatically updates via INotifyPropertyChanged.
/// When FullName is accessed, it reflects the latest FirstName/LastName values.
/// 
/// COMPUTED PROPERTIES:
/// ====================
/// Properties like FullName and Age are computed from other properties.
/// When FirstName or LastName changes, we call OnPropertyChanged(nameof(FullName))
/// to notify bindings that FullName has also changed.
/// </summary>
[Table(Database = "AppData", IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
    // ==================================================================================
    // Private backing fields for properties that use SetProperty()
    // ==================================================================================

    private string? _email;
    private string? _passwordHash;
    private string? _firstName;
    private string? _lastName;
    private DateTime? _dateOfBirth;
    private DateTime _createdAt;
    private DateTime _lastLoginAt;

    // ==================================================================================
    // Database-mapped properties with SetProperty() for two-way binding
    // ==================================================================================

    /// <summary>
    /// User's email address (unique identifier).
    /// SetProperty() automatically raises PropertyChanged event for UI binding.
    /// </summary>
    [UniqueIndex]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    /// <summary>
    /// Hashed password (never store plain text passwords).
    /// SetProperty() enables two-way binding with password entry fields.
    /// </summary>
    public string? PasswordHash
    {
        get => _passwordHash;
        set => SetProperty(ref _passwordHash, value);
    }

    /// <summary>
    /// User's first name.
    /// When this changes, we also notify FullName because it depends on FirstName.
    /// </summary>
    [Required(ErrorMessage = "First name is required")]
    public string? FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                // Notify dependent computed property
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    /// <summary>
    /// User's last name.
    /// When this changes, we also notify FullName because it depends on LastName.
    /// </summary>
    [Required(ErrorMessage = "Last name is required")]
    public string? LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
            {
                // Notify dependent computed property
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    /// <summary>
    /// User's date of birth.
    /// When this changes, we also notify Age because it's computed from DateOfBirth.
    /// </summary>
    public DateTime? DateOfBirth
    {
        get => _dateOfBirth;
        set
        {
            if (SetProperty(ref _dateOfBirth, value))
            {
                // Notify dependent computed property
                OnPropertyChanged(nameof(Age));
            }
        }
    }

    /// <summary>
    /// Timestamp when the user was created.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    /// <summary>
    /// Timestamp of last login.
    /// </summary>
    public DateTime LastLoginAt
    {
        get => _lastLoginAt;
        set => SetProperty(ref _lastLoginAt, value);
    }

    // ==================================================================================
    // Computed properties (not stored in database)
    // ==================================================================================

    /// <summary>
    /// Gets the user's full name for display (computed from FirstName and LastName).
    /// 
    /// BINDING NOTE:
    /// This property doesn't have a backing field because it's computed on-demand.
    /// When FirstName or LastName changes, those setters call OnPropertyChanged(nameof(FullName))
    /// to notify any UI bindings that FullName has also changed.
    /// 
    /// XAML Example: &lt;Label Text="{Binding CurrentUser.FullName}" /&gt;
    /// This label automatically updates when FirstName or LastName changes!
    /// </summary>
    [NotColumn]
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Gets the user's age based on date of birth (computed property).
    /// 
    /// BINDING NOTE:
    /// When DateOfBirth changes, the setter calls OnPropertyChanged(nameof(Age))
    /// to notify any UI bindings that Age has also changed.
    /// 
    /// XAML Example: &lt;Label Text="{Binding CurrentUser.Age}" /&gt;
    /// This label automatically updates when DateOfBirth changes!
    /// </summary>
    [NotColumn]
    public int? Age
    {
        get
        {
            if (DateOfBirth == null) return null;

            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Value.Year;
            if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    // ==================================================================================
    // Validation helper
    // ==================================================================================

    /// <summary>
    /// Validates the entity using data annotations.
    /// Returns true if valid, false otherwise.
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, true))
        {
            errors.AddRange(results.Select(r => r.ErrorMessage ?? "Validation error"));
            return false;
        }

        return true;
    }
}
