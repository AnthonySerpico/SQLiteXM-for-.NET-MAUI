using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a line item in an invoice (track purchase)
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class InvoiceLine : SxmEntity
{
    [Required]
    [ForeignKey(foreignTable: nameof(Invoice))]
    public long InvoiceId { get; set; }

    [Required]
    [ForeignKey(foreignTable: nameof(Track))]
    public long TrackId { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [Required]
    public int Quantity { get; set; }
}
