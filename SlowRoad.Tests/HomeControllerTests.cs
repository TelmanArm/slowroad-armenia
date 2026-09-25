using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SlowRoad.Controllers;
using SlowRoad.Data;

namespace SlowRoad.Tests;

public class HomeControllerTests
{
    [Fact] // Index() should return a view
    public async Task Index_ReturnsView()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(Index_ReturnsView))
            .Options;
        using var db = new AppDbContext(options);

        var controller = new HomeController(NullLogger<HomeController>.Instance, db);
        var result = await controller.Index();
        Assert.IsType<ViewResult>(result);
    }
}
