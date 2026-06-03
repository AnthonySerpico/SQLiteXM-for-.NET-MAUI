using SQLiteXM;
using System.ComponentModel.DataAnnotations;

namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents a music genre (Rock, Jazz, Classical, etc.)
/// </summary>
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
public class Genre : SxmEntity
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
