using System.ComponentModel.DataAnnotations;

namespace SlowRoad.Models;

public class Section
{
    public int Id { get; set; }

    [Required]
    [StringLength(80)]
    public string Key { get; set; } = "";

    [StringLength(120)]
    public string? Eyebrow { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Lead { get; set; }

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; }

    public ICollection<SectionItem> Items { get; set; } = new List<SectionItem>();
}
