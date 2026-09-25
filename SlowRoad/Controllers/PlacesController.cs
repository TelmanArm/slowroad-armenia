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
    public async Task<IActionResult> Edit(int id, Place input)
    {
        // The edit form doesn't include Slug (it's the fixed image identifier
        // and must not change), so its Required check would otherwise block save.
        ModelState.Remove(nameof(Place.Slug));

        // Photos are managed by their own actions below (AddPhotos, etc.).
        var place = await _db.Places
            .Include(p => p.Photos)
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
        place.ShortDescription = input.ShortDescription;
        place.Description = input.Description;

        // Detail page fields
        place.Body = input.Body;
        place.Highlights = input.Highlights;
        place.GettingThere = input.GettingThere;
        place.Tips = input.Tips;
        place.DistanceFromYerevan = input.DistanceFromYerevan;
        place.TimeNeeded = input.TimeNeeded;
        place.BestTime = input.BestTime;
        place.Latitude = input.Latitude;
        place.Longitude = input.Longitude;

        await _db.SaveChangesAsync();

        // Saved → leave the edit screen and go back to the list.
        return RedirectToAction(nameof(Index));
    }
    
    // ================= Photos =================
    // A place can have many photos. They are shown in SortOrder:
    // the FIRST one is the main photo (home page card + detail page hero),
    // all of them appear in the photo slider on the detail page.

    // POST /Places/AddPhotos/5  → upload one or more photos, added at the end.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]                               // ~100 MB per upload
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
    public async Task<IActionResult> AddPhotos(int id, List<IFormFile> photos)
    {
        var place = await _db.Places
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (place == null)
            return NotFound();

        var next = place.Photos.Count == 0 ? 0 : place.Photos.Max(p => p.SortOrder) + 1;
        var added = 0;
        var skipped = new List<string>();

        foreach (var upload in photos.Where(f => f.Length > 0))
        {
            if (!upload.ContentType.StartsWith("image/"))
            {
                skipped.Add(upload.FileName);
                continue;
            }

            using var ms = new MemoryStream();
            await upload.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var photo = new Photo { Alt = place.Name, SortOrder = next++ };
            foreach (var width in PhotoWidths)
                photo.Files.Add(new PhotoFile { Width = width, ContentType = upload.ContentType, Bytes = bytes });

            place.Photos.Add(photo);
            added++;
        }

        await _db.SaveChangesAsync();

        TempData["PhotoMessage"] = added == 0 ? "No photos were added."
            : added == 1 ? "1 photo added." : $"{added} photos added.";
        if (skipped.Count > 0)
            TempData["PhotoError"] = "Not an image, skipped: " + string.Join(", ", skipped);

        return RedirectToAction(nameof(Edit), new { id });
    }

    // POST /Places/MakeMainPhoto/5  (photoId in the form) → move it to the front.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeMainPhoto(int id, int photoId)
    {
        var photos = await LoadPhotosAsync(id);
        var target = photos.FirstOrDefault(p => p.Id == photoId);
        if (target == null)
            return NotFound();

        photos.Remove(target);
        photos.Insert(0, target);
        Renumber(photos);
        await _db.SaveChangesAsync();

        TempData["PhotoMessage"] = "Main photo changed.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // POST /Places/MovePhoto/5  (photoId, step = -1 left / +1 right) → reorder.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MovePhoto(int id, int photoId, int step)
    {
        var photos = await LoadPhotosAsync(id);
        var from = photos.FindIndex(p => p.Id == photoId);
        if (from < 0)
            return NotFound();

        var to = Math.Clamp(from + Math.Sign(step), 0, photos.Count - 1);
        (photos[from], photos[to]) = (photos[to], photos[from]);
        Renumber(photos);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    // POST /Places/DeletePhoto/5  (photoId) → delete one photo and its files.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(int id, int photoId)
    {
        var photos = await LoadPhotosAsync(id);
        var target = photos.FirstOrDefault(p => p.Id == photoId);
        if (target == null)
            return NotFound();

        _db.Photos.Remove(target);   // its PhotoFiles go too (cascade delete)
        photos.Remove(target);
        Renumber(photos);
        await _db.SaveChangesAsync();

        TempData["PhotoMessage"] = "Photo deleted.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    // This place's photos in display order (tracked, so changes get saved).
    private Task<List<Photo>> LoadPhotosAsync(int placeId) =>
        _db.Photos
            .Where(p => p.ItemId == placeId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

    // Rewrite SortOrder as 0, 1, 2… so the first photo is always 0 (= main).
    private static void Renumber(List<Photo> photos)
    {
        for (var i = 0; i < photos.Count; i++)
            photos[i].SortOrder = i;
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