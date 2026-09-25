using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlowRoad.Data;
using SlowRoad.Models;

namespace SlowRoad.Controllers;

[Authorize]
public class PlacesController : Controller
{
    private readonly AppDbContext _db;

    public PlacesController(AppDbContext db)
    {
        _db = db;
    }
    
    // GET /Places
    public async Task<IActionResult> Index()
    {
        var places = await _db.Places
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        return View(places);
    }
    
    // GET /Places/Create
    public IActionResult Create()
    {
        return View(new Place());
    }

    // POST /Places/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Place place)
    {
        if (!ModelState.IsValid)
            return View(place);

        _db.Places.Add(place);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    
    // The two widths we keep for each photo (see PhotosController / srcset).
    private static readonly int[] PhotoWidths = { 700, 1400 };

    // GET /Places/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var place = await _db.Places
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (place == null)
            return NotFound();

        return View(place);
    }

    // POST /Places/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Place input, IFormFile? photo)
    {
        // The edit form doesn't include Slug (it's the fixed image identifier
        // and must not change), so its Required check would otherwise block save.
        ModelState.Remove(nameof(Place.Slug));

        if (photo is { Length: > 0 } && !photo.ContentType.StartsWith("image/"))
            ModelState.AddModelError("photo", "Please choose an image file.");

        var place = await _db.Places
            .Include(p => p.Photos)
                .ThenInclude(p => p.Files)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (place == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            // Re-render with the current photo shown.
            input.Photos = place.Photos;
            return View(input);
        }

        place.Name = input.Name;
        place.Region = input.Region;
        place.Description = input.Description;

        // Optional new image: replace this place's photo bytes for both widths.
        if (photo is { Length: > 0 })
        {
            using var ms = new MemoryStream();
            await photo.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var target = place.Photos.OrderBy(p => p.SortOrder).FirstOrDefault();
            if (target == null)
            {
                target = new Photo { Alt = place.Name, SortOrder = 0 };
                place.Photos.Add(target);
            }

            foreach (var width in PhotoWidths)
            {
                var file = target.Files.FirstOrDefault(f => f.Width == width);
                if (file == null)
                {
                    file = new PhotoFile { Width = width };
                    target.Files.Add(file);
                }
                file.ContentType = photo.ContentType;
                file.Bytes = bytes;
            }
        }

        await _db.SaveChangesAsync();

        // Saved → leave the edit screen and go back to the list.
        return RedirectToAction(nameof(Index));
    }
    
    // GET /Places/Delete/5  → confirm page
    public async Task<IActionResult> Delete(int id)
    {
        var place = await _db.Places.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (place == null)
            return NotFound();

        return View(place);
    }

    // POST /Places/Delete/5  → actually delete
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var place = await _db.Places.FindAsync(id);
        if (place != null)
        {
            _db.Places.Remove(place);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

}