using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlowRoad.Data;
using SlowRoad.Models;

namespace SlowRoad.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _db;

    public HomeController(ILogger<HomeController> logger, AppDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        // Load the published sections and their places from the database, keyed
        // by Key so the views can pick out "featured" / "destinations".
        var sections = await _db.Sections
            .AsNoTracking()
            .AsSplitQuery()
            .Where(s => s.IsPublished)
            .OrderBy(s => s.SortOrder)
            .Include(s => s.Items.OrderBy(i => i.SortOrder))
                .ThenInclude(i => i.Item)
                    .ThenInclude(c => c.Photos.OrderBy(p => p.SortOrder))
            .ToListAsync();

        var model = sections.ToDictionary(s => s.Key);
        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
