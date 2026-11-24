using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace SlowRoad.Tests;

// The admin surface. Everything behind [Authorize] must stay behind it, and
// the login form must keep its antiforgery and open-redirect guards.
public class AdminAuthTests : IClassFixture<TestAppFactory>
{
    private const string Email = "editor@test.local";
    private const string Password = "Test-Passw0rd!";

    private readonly TestAppFactory _factory;

    public AdminAuthTests(TestAppFactory factory)
    {
        _factory = factory;

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (users.FindByEmailAsync(Email).GetAwaiter().GetResult() is null)
        {
            var user = new IdentityUser { UserName = Email, Email = Email, EmailConfirmed = true };
            var created = users.CreateAsync(user, Password).GetAwaiter().GetResult();
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        }
    }

    // A client that shows us the redirect instead of following it.
    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    // ---- the gate ----------------------------------------------------------

    [Theory] // every admin page sends an anonymous visitor to the login form
    [InlineData("/Admin")]
    [InlineData("/Places")]
    [InlineData("/Places/Create")]
    [InlineData("/Places/Edit/1")]
    [InlineData("/Places/Delete/1")]
    public async Task AdminPages_RequireLogin(string url)
    {
        var response = await NoRedirectClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Admin/Login", response.Headers.Location?.OriginalString);
    }

    [Fact] // the login form itself stays open, or nobody can get in
    public async Task LoginPage_IsAnonymous()
    {
        var response = await _factory.CreateClient().GetAsync("/Admin/Login");
        response.EnsureSuccessStatusCode();
    }

    [Fact] // a write action without a session is refused, not performed
    public async Task PostWithoutLogin_IsRefused()
    {
        var response = await NoRedirectClient().PostAsync("/Places/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "Injected",
                ["Slug"] = "injected",
            }));

        Assert.True(
            response.StatusCode is HttpStatusCode.Found or HttpStatusCode.BadRequest,
            $"expected a redirect to login or a 400, got {(int)response.StatusCode}");
    }

    // ---- the login form ----------------------------------------------------

    [Fact] // a post with no antiforgery token is rejected
    public async Task Login_WithoutAntiforgeryToken_IsRejected()
    {
        var response = await _factory.CreateClient().PostAsync("/Admin/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = Email,
                ["Password"] = Password,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact] // the right password gets a session and reaches the admin home
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        var client = NoRedirectClient();
        var response = await PostLoginAsync(client, Email, Password);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Admin", response.Headers.Location?.OriginalString);

        // The session works: the gated page now renders instead of redirecting.
        var admin = await client.GetAsync("/Admin");
        admin.EnsureSuccessStatusCode();
    }

    [Fact] // a wrong password re-renders the form and grants nothing
    public async Task Login_WithWrongPassword_Fails()
    {
        var client = NoRedirectClient();
        var response = await PostLoginAsync(client, Email, "not-the-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // the form, again
        Assert.Contains("Invalid email or password.", await response.Content.ReadAsStringAsync());

        var admin = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Found, admin.StatusCode);
    }

    [Fact] // an unknown email fails the same way, with the same message
    public async Task Login_WithUnknownEmail_Fails()
    {
        var response = await PostLoginAsync(NoRedirectClient(), "nobody@test.local", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password.", await response.Content.ReadAsStringAsync());
    }

    [Fact] // ReturnUrl must not become an open redirect off the site
    public async Task Login_IgnoresOffsiteReturnUrl()
    {
        var client = NoRedirectClient();
        var response = await PostLoginAsync(client, Email, Password, returnUrl: "https://evil.example.com/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Admin", response.Headers.Location?.OriginalString);
    }

    [Fact] // a local ReturnUrl is honoured, so the login redirect still works
    public async Task Login_HonoursLocalReturnUrl()
    {
        var client = NoRedirectClient();
        var response = await PostLoginAsync(client, Email, Password, returnUrl: "/Places");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Places", response.Headers.Location?.OriginalString);
    }

    // ---- helpers -----------------------------------------------------------

    // Fetches the login form, carries its antiforgery token and cookie back in,
    // the way a browser does.
    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string email, string password, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/Admin/Login" : $"/Admin/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var form = await client.GetAsync(url);
        form.EnsureSuccessStatusCode();

        var html = await form.Content.ReadAsStringAsync();
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "no antiforgery token on the login form");

        var fields = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        };
        if (returnUrl is not null)
            fields["ReturnUrl"] = returnUrl;

        var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/Login")
        {
            Content = new FormUrlEncodedContent(fields),
        };
        foreach (var cookie in Cookies(form))
            request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request);
    }

    // name=value pairs from Set-Cookie, dropping the attributes.
    private static IEnumerable<string> Cookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? values.Select(v => v.Split(';')[0])
            : Enumerable.Empty<string>();

    private static class HeaderNames
    {
        public const string SetCookie = "Set-Cookie";
    }
}
