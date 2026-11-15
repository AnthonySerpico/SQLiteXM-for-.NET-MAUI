using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a line item in an invoice (track purchase)
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[Index("InvoiceId", "TrackId")]
[Index("TrackId", "InvoiceId")]
public class InvoiceLine : SxmEntity
{
    [Required]
    [ForeignKey(foreignTable: nameof(Invoice))]
    [Index]
    public long InvoiceId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="InvoiceId"/>. Populated by LoadWith; otherwise null.
    /// </summary>
    [NotColumn]
    public Invoice? Invoice { get; set; }

    [Required]
    [ForeignKey(foreignTable: nameof(Track))]
    [Index]
    public long TrackId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="TrackId"/>. Populated by LoadWith; otherwise null.
    /// </summary>
    [NotColumn]
    public Track? Track { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [Required]
    public int Quantity { get; set; }
}
