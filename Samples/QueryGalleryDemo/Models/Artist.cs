using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a music artist or band
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class Artist : SxmEntity
{
    [Required]
    [UniqueIndex]
    public string Name { get; set; } = string.Empty;
}
