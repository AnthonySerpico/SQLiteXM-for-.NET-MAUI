using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Junction table for many-to-many relationship between Playlist and Track
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class PlaylistTrack : SxmEntity
{
    [Required]
    [ForeignKey(foreignTable: nameof(Playlist))]
    public long PlaylistId { get; set; }

    [Required]
    [ForeignKey(foreignTable: nameof(Track))]
    public long TrackId { get; set; }
}
