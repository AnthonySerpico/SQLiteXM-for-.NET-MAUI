using SQLiteXM;

namespace DirectBindingDemo.Models;

/// <summary>
/// User preferences entity with direct binding support.
/// Demonstrates a related entity with foreign key to User.
/// 
/// Like User, this entity uses SetProperty() for all mutable properties
/// to enable direct UI binding.
/// </summary>
[Table(Database = "AppData", IsColumnAttributeRequired = false)]
public class UserPreferences : SxmEntity
{
    private long _userId;
    private bool _enableNotifications;
    private string? _referralCode;
    private DateTime _createdAt;

    /// <summary>
    /// Foreign key to the User entity.
    /// </summary>
    [Index]
    public long UserId
    {
        get => _userId;
        set => SetProperty(ref _userId, value);
    }

    /// <summary>
    /// Whether the user wants to receive notifications.
    /// Direct binding example: &lt;Switch IsToggled="{Binding UserPreferences.EnableNotifications}" /&gt;
    /// </summary>
    public bool EnableNotifications
    {
        get => _enableNotifications;
        set => SetProperty(ref _enableNotifications, value);
    }

    /// <summary>
    /// Optional referral code provided during registration.
    /// </summary>
    public string? ReferralCode
    {
        get => _referralCode;
        set => SetProperty(ref _referralCode, value);
    }

    /// <summary>
    /// Timestamp when preferences were created.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }
}
