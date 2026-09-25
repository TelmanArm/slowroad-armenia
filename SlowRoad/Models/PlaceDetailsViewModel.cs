namespace SlowRoad.Models;

// Everything the destination detail page (/destinations/{slug}) renders.
public class PlaceDetailsViewModel
{
    public Place Place { get; set; } = null!;

    // Photos in display order; the first one is the hero image.
    public List<Photo> Photos { get; set; } = new();

    // "More in <region>" cards at the bottom of the page.
    public List<PlaceCardViewModel> MorePlaces { get; set; } = new();
    public string MorePlacesTitle { get; set; } = "More destinations";

    // Splits a text block on empty lines → paragraphs.
    public static IEnumerable<string> Paragraphs(string? text) =>
        (text ?? "").Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Splits a text block on line breaks → list items (leading "-" or "•" removed).
    public static IEnumerable<string> Lines(string? text) =>
        (text ?? "").Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('-', '•', '*', ' '))
            .Where(l => l.Length > 0);
}
