using System.ComponentModel.DataAnnotations;

namespace SlowRoad.Models;

public abstract class ContentItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = "";

    [Required]
    [StringLength(140)]
    public string Slug { get; set; } = "";

    [StringLength(80)]
    public string? Region { get; set; }

    [StringLength(300)]
    public string? ShortDescription { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsPublished { get; set; }

    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
}
