using Microsoft.AspNetCore.Mvc.Testing;

namespace SlowRoad.Tests;

// Starts the whole app in memory and calls its pages (smoke test)
public class HomePageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomePageTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact] // homepage "/" should load without error
    public async Task Homepage_ReturnsSuccess()
    {
        var client = _factory.CreateClient();      // fake browser
        var response = await client.GetAsync("/"); // visit homepage
        response.EnsureSuccessStatusCode();        // must be 200 OK
    }
}