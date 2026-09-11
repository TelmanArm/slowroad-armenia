using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SlowRoad.Controllers;

namespace SlowRoad.Tests;

public class HomeControllerTests
{
    [Fact] // Index() should return a view
    public void Index_ReturnsView()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance); // fake logger
        var result = controller.Index();
        Assert.IsType<ViewResult>(result);
    }
}
