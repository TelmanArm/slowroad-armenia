using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SlowRoad.Data;
using SlowRoad.Models;

namespace SlowRoad.Tests;

// Smoke tests for the public place detail page: /destinations/{slug}
public class DestinationsPageTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public DestinationsPageTests(TestAppFactory factory)
    {
        _factory = factory;

        // Seed one published place into the in-memory database (once).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Places.Any(p => p.Slug == "test-dilijan"))
        {
            db.Places.Add(new Place
            {
                Name = "Test Dilijan",
                Slug = "test-dilijan",
                Region = "Tavush",
                ShortDescription = "Forested spa town.",
                Highlights = "Old Dilijan\nHaghartsin Monastery",
                Latitude = 40.7406,
                Longitude = 44.8631,
            });
            db.SaveChanges();
        }
    }

    [Fact] // a known slug renders the page with the place name
    public async Task KnownPlace_ReturnsPage()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/destinations/test-dilijan");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Dilijan", html);
        Assert.Contains("Haghartsin Monastery", html);
    }

    [Fact] // an unknown slug is a 404, not an error page
    public async Task UnknownPlace_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/destinations/no-such-place");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
