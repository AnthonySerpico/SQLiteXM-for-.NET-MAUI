using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a media type (MPEG, AAC, MP3, etc.)
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class MediaType : SxmEntity
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
