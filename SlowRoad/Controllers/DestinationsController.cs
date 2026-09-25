using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlowRoad.Data;
using SlowRoad.Models;

namespace SlowRoad.Controllers;

// Public detail page for one place: GET /destinations/{slug}  (e.g. /destinations/dilijan)
[Route("destinations")]
public class DestinationsController : Controller
{
    private const int MorePlacesCount = 3;

    private readonly AppDbContext _db;

    public DestinationsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /destinations → the list lives on the home page.
    [HttpGet("")]
    public IActionResult Index() =>
        RedirectToAction("Index", "Home", null, "destinations");

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        slug = slug.Trim().ToLowerInvariant();

        var place = await _db.Places
            .AsNoTracking()
            .Include(p => p.Photos.OrderBy(ph => ph.SortOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);

        if (place is null)
            return NotFound();

        // "More places": same region first, then fill up with any others.
        var region = place.Region;
        var more = new List<Place>();
        if (!string.IsNullOrEmpty(region))
        {
            more = await _db.Places
                .AsNoTracking()
                .Include(p => p.Photos.OrderBy(ph => ph.SortOrder))
                .Where(p => p.IsPublished && p.Id != place.Id && p.Region == region)
                .OrderBy(p => p.Name)
                .Take(MorePlacesCount)
                .ToListAsync();
        }

        var sameRegion = more.Count > 0;
        if (more.Count < MorePlacesCount)
        {
            var skipIds = more.Select(p => p.Id).Append(place.Id).ToList();
            more.AddRange(await _db.Places
                .AsNoTracking()
                .Include(p => p.Photos.OrderBy(ph => ph.SortOrder))
                .Where(p => p.IsPublished && !skipIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .Take(MorePlacesCount - more.Count)
                .ToListAsync());
        }

        var model = new PlaceDetailsViewModel
        {
            Place = place,
            Photos = place.Photos.OrderBy(p => p.SortOrder).ToList(),
            MorePlaces = more
                .Select(p => PlaceCardViewModel.From(p, p.ShortDescription ?? p.Description))
                .ToList(),
            MorePlacesTitle = sameRegion && more.All(p => p.Region == region)
                ? $"More in {region}"
                : "More destinations",
        };

        return View(model);
    }
}
