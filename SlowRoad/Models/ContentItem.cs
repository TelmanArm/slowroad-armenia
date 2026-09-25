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
    [Display(Name = "Short description (one sentence, shown on cards and under the title)")]
    public string? ShortDescription { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public bool IsPublished { get; set; } = true;

    public ICollection<Photo> Photos { get; set; } = new List<Photo>();

    // ---- Detail page (/destinations/{slug}) --------------------------------
    // All optional: the detail page hides any block that is left empty.

    // Long article. Leave an empty line between paragraphs.
    [StringLength(10000)]
    [Display(Name = "Full story (empty line between paragraphs)")]
    public string? Body { get; set; }

    // One highlight per line.
    [StringLength(2000)]
    [Display(Name = "Highlights (one per line)")]
    public string? Highlights { get; set; }

    [StringLength(2000)]
    [Display(Name = "Getting there")]
    public string? GettingThere { get; set; }

    // One tip per line.
    [StringLength(2000)]
    [Display(Name = "Practical tips (one per line)")]
    public string? Tips { get; set; }

    // "At a glance" facts, free text, e.g. "100 km · about 1h 40m by car".
    [StringLength(80)]
    [Display(Name = "Distance from Yerevan")]
    public string? DistanceFromYerevan { get; set; }

    [StringLength(80)]
    [Display(Name = "Time needed")]
    public string? TimeNeeded { get; set; }

    [StringLength(80)]
    [Display(Name = "Best time to visit")]
    public string? BestTime { get; set; }

    // Used for the map and the "Open in Google Maps" link.
    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }
}
