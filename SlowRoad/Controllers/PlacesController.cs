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
    
    // GET /Places/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var place = await _db.Places.FindAsync(id);
        if (place == null)
            return NotFound();

        return View(place);
    }

    // POST /Places/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Place input)
    {
        if (!ModelState.IsValid)
            return View(input);

        var place = await _db.Places.FindAsync(id);
        if (place == null)
            return NotFound();

        place.Name = input.Name;
        place.Region = input.Region;
        place.Description = input.Description;

        await _db.SaveChangesAsync();
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