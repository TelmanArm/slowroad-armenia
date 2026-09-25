namespace SlowRoad.Models;

// The bits a single destination card needs to render. Built in _Featured /
// _Destinations from a SectionItem's ContentItem and its first photo.
public class PlaceCardViewModel
{
    public int PhotoId { get; set; }
    public string? Alt { get; set; }
    public string? Region { get; set; }
    public string Name { get; set; } = "";
    public string? Body { get; set; }
}
