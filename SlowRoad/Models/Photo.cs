using System.ComponentModel.DataAnnotations;

namespace SlowRoad.Models;

public class Photo
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public ContentItem Item { get; set; } = null!;

    [StringLength(200)]
    public string? Alt { get; set; }

    public int SortOrder { get; set; }

    public ICollection<PhotoFile> Files { get; set; } = new List<PhotoFile>();
}
