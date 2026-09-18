using System.ComponentModel.DataAnnotations;

namespace SlowRoad.Models;

public class Place
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = "";

    [StringLength(80)]
    public string? Region { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}