using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a customer
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? Company { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Support representative (Employee)
    /// </summary>
    [ForeignKey(foreignTable: nameof(Employee))]
    public long? SupportRepId { get; set; }
}
