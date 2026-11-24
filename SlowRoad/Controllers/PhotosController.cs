using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlowRoad.Data;

namespace SlowRoad.Controllers;

[Route("photos")]
public class PhotosController : Controller
{
    private readonly AppDbContext _db;

    public PhotosController(AppDbContext db)
    {
        _db = db;
    }

    // Only these are ever served back.
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

    // GET /photos/{id}/{width} → the stored image bytes for that photo and width.
    [HttpGet("{id:int}/{width:int}")]
    [ResponseCache(Duration = 604800)] // 7 days
    public async Task<IActionResult> Get(int id, int width)
    {
        var file = await _db.PhotoFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.PhotoId == id && f.Width == width);

        // No row for this width: use the widest we have.
        file ??= await _db.PhotoFiles
            .AsNoTracking()
            .OrderByDescending(f => f.Width)
            .FirstOrDefaultAsync(f => f.PhotoId == id);

        if (file is null)
            return NotFound();

        if (!AllowedContentTypes.Contains(file.ContentType))
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Bytes, file.ContentType);
    }
}
