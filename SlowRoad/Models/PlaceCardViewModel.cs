namespace SlowRoad.Models;

// The bits a single destination card needs to render. Built in _Featured /
// _Destinations (and the "More places" block of a detail page) from a
// ContentItem and its first photo.
public class PlaceCardViewModel
{
    public int PhotoId { get; set; }
    public string? Alt { get; set; }
    public string? Region { get; set; }
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public string? Body { get; set; }

    public static PlaceCardViewModel From(ContentItem item, string? body)
    {
        var photo = item.Photos.OrderBy(p => p.SortOrder).FirstOrDefault();
        return new PlaceCardViewModel
        {
            PhotoId = photo?.Id ?? 0,
            Alt = photo?.Alt,
            Region = item.Region,
            Name = item.Name,
            Slug = item is Place ? item.Slug : null, // only places have a detail page (for now)
            Body = body,
        };
    }
}
