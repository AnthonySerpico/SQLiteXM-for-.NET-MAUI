using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a music album
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[Index("ArtistId", "Title")]
public class Album : SxmEntity
{
    [Required]
    [Index]
    public string Title { get; set; } = string.Empty;

    [Required]
    [ForeignKey(foreignTable: nameof(Artist))]
    [Index]
    public long ArtistId { get; set; }
}
