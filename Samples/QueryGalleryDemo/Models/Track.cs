using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a music track (song)
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class Track : SxmEntity
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [ForeignKey(foreignTable: nameof(Album))]
    [Index]
    public long? AlbumId { get; set; }

    [ForeignKey(foreignTable: nameof(MediaType))]
    [Index]
    [Required]
    public long MediaTypeId { get; set; }

    [ForeignKey(foreignTable: nameof(Genre))]
    [Index]
    public long? GenreId { get; set; }

    public string? Composer { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    [Required]
    public int Milliseconds { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public int? Bytes { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    public int? TrackNumber { get; set; }
}
