using SQLiteXM;

namespace RegistrationDemo.Models;

/// <summary>
/// Represents a registered user, stored in the UserData database.
/// </summary>
[Table(Database = "UserData", IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
    [UniqueIndex]
    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastLoginAt { get; set; }

    /// <summary>
    /// Gets the user's full name for display.
    /// </summary>
    [NotColumn]
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Gets the user's age based on date of birth.
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
}
