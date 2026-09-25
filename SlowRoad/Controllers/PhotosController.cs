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

    // GET /photos/{id}/{width} → the stored image bytes for that photo and width.
    [HttpGet("{id:int}/{width:int}")]
    [ResponseCache(Duration = 604800)] // 7 days
    public async Task<IActionResult> Get(int id, int width)
    {
        var file = await _db.PhotoFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.PhotoId == id && f.Width == width);

        if (file is null)
            return NotFound();

        return File(file.Bytes, file.ContentType);
    }
}
