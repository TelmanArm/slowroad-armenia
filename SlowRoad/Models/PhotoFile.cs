using System.ComponentModel.DataAnnotations;

namespace SlowRoad.Models;

public class PhotoFile
{
    public int Id { get; set; }

    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;

    public int Width { get; set; }

    [Required]
    [StringLength(100)]
    public string ContentType { get; set; } = "";

    public byte[] Bytes { get; set; } = Array.Empty<byte>();
}
