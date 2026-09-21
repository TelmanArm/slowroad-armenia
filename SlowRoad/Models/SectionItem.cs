namespace SlowRoad.Models;

public class SectionItem
{
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public int ItemId { get; set; }
    public ContentItem Item { get; set; } = null!;

    public int SortOrder { get; set; }
}
