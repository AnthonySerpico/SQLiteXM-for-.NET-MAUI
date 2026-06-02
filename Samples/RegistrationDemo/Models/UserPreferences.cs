using SQLiteXM;

namespace RegistrationDemo.Models;

/// <summary>
/// User preferences and settings, stored in the UserData database.
/// </summary>
[Table(Database = "UserData", IsColumnAttributeRequired = false)]
public class UserPreferences : SxmEntity
{
    [ForeignKey(nameof(User))]
    [Index]
    public long UserId { get; set; }

    public bool EnableNotifications { get; set; }

    public string? ReferralCode { get; set; }

    public DateTime CreatedAt { get; set; }
}
