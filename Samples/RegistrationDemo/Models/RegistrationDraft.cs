using SQLiteXM;

namespace RegistrationDemo.Models;

/// <summary>
/// Represents a registration in progress, stored in the Session database.
/// This allows users to resume registration if they close the app mid-flow.
/// </summary>
[Table(Database = "Session", IsColumnAttributeRequired = false)]
public class RegistrationDraft : SxmEntity
{
    // Page 1: Email & Password
    [UniqueIndex]
    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    // Page 2: Personal Info
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    // Page 3: Preferences
    public bool AcceptedTerms { get; set; }

    public bool EnableNotifications { get; set; }

    public string? ReferralCode { get; set; }

    // Tracking
    public int CompletedStep { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime LastUpdated { get; set; }
}
