using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents an employee
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class Employee : SxmEntity
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? Title { get; set; }

    /// <summary>
    /// Reports to employee (self-referencing foreign key)
    /// </summary>
    [ForeignKey(foreignTable: nameof(Employee))]
    [Index]
    public long? ReportsTo { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }

    public string? Phone { get; set; }

    public string? Fax { get; set; }

    public string? Email { get; set; }
}
