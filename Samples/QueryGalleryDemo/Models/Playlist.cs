using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a playlist
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class Playlist : SxmEntity
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
