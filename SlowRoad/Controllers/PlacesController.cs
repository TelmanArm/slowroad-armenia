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
    
    // One row per photo. No resizing library yet, so storing it per width
    // only duplicated the bytes. Smaller widths fall back to this one.
    private const int FullWidth = 1400;

    // Type from the first bytes, never the client's ContentType
    // (image/svg+xml there is stored XSS). null = not allowed.
    private static string? SniffImageContentType(byte[] b)
    {
        // JPEG: FF D8 FF
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return "image/png";

        // WebP: "RIFF" .... "WEBP"
        if (b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
            && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P')
            return "image/webp";

        return null;
    }

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
            using var ms = new MemoryStream();
            await upload.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var contentType = SniffImageContentType(bytes);
            if (contentType is null)
            {
                skipped.Add(upload.FileName);
                continue;
            }

            var photo = new Photo { Alt = place.Name, SortOrder = next++ };
            photo.Files.Add(new PhotoFile { Width = FullWidth, ContentType = contentType, Bytes = bytes });

            place.Photos.Add(photo);
            added++;
        }

        await _db.SaveChangesAsync();

        TempData["PhotoMessage"] = added == 0 ? "No photos were added."
            : added == 1 ? "1 photo added." : $"{added} photos added.";
        if (skipped.Count > 0)
            TempData["PhotoError"] = "Not a JPEG, PNG or WebP image, skipped: " + string.Join(", ", skipped);

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