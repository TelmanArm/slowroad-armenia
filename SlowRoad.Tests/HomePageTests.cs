namespace SlowRoad.Tests;

// Starts the whole app in memory (with an in-memory database) and calls its
// pages (smoke test)
public class HomePageTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public HomePageTests(TestAppFactory factory) => _factory = factory;

    [Fact] // homepage "/" should load without error
    public async Task Homepage_ReturnsSuccess()
    {
        var client = _factory.CreateClient();      // fake browser
        var response = await client.GetAsync("/"); // visit homepage
        response.EnsureSuccessStatusCode();        // must be 200 OK
    }
}
