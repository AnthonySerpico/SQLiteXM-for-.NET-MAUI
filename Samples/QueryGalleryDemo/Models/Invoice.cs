using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a customer invoice
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[Index("CustomerId", "InvoiceDate")]
public class Invoice : SxmEntity
{
    [Required]
    [ForeignKey(foreignTable: nameof(Customer))]
    [Index]
    public long CustomerId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="CustomerId"/>. Populated by LoadWith; otherwise null.
    /// </summary>
    [NotColumn]
    public Customer? Customer { get; set; }

    [Required]
    [Index]
    public DateTime InvoiceDate { get; set; }

    public string? BillingAddress { get; set; }

    public string? BillingCity { get; set; }

    public string? BillingState { get; set; }

    public string? BillingCountry { get; set; }

    public string? BillingPostalCode { get; set; }

    [Required]
    public decimal Total { get; set; }
}
