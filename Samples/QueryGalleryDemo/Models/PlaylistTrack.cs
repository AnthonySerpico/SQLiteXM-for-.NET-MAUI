using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Junction table for many-to-many relationship between Playlist and Track
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[UniqueIndex("PlaylistId", "TrackId")]
[Index("TrackId", "PlaylistId")]
public class PlaylistTrack : SxmEntity
{
    [Required]
    [ForeignKey(foreignTable: nameof(Playlist), OnDelete = ForeignKeyDeleteAction.Cascade)]
    [Index]
    public long PlaylistId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="PlaylistId"/>. Populated by LoadWith; otherwise null.
    /// </summary>
    [NotColumn]
    public Playlist? Playlist { get; set; }

    [Required]
    [ForeignKey(foreignTable: nameof(Track), OnDelete = ForeignKeyDeleteAction.Cascade)]
    [Index]
    public long TrackId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="TrackId"/>. Populated by LoadWith; otherwise null.
    /// </summary>
    [NotColumn]
    public Track? Track { get; set; }
}
