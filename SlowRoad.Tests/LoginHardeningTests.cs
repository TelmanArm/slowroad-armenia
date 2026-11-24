using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlowRoad.Security;

namespace SlowRoad.Tests;

// /Admin/Login used to accept unlimited password guesses: lockoutOnFailure was
// false and nothing throttled the endpoint. These tests hold both controls in
// place. Each class seeds its own account, because the in-memory store is
// shared across factories and lockout is per-user state.

// ---- the per-IP throttle ---------------------------------------------------

public class LoginRateLimitTests : IClassFixture<LoginRateLimitTests.Factory>
{
    private const string Email = "ratelimit@test.local";
    private const string Password = "Test-Passw0rd!";

    public sealed class Factory : TestAppFactory
    {
        public Factory()
        {
            Settings["Security:Login:PermitLimit"] = "3";
            Settings["Security:Login:WindowMinutes"] = "5";
            // Out of the way, so only the throttle can end this test.
            Settings["Security:Login:MaxFailedAttempts"] = "100";
        }
    }

    private readonly Factory _factory;

    public LoginRateLimitTests(Factory factory)
    {
        _factory = factory;
        Seed.User(factory, Email, Password);
    }

    [Fact] // guess past the per-window limit and the endpoint stops answering
    public async Task Login_StopsAcceptingAttempts_PastTheLimit()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // The permitted attempts still reach the app and still fail.
        for (var i = 1; i <= 3; i++)
        {
            var allowed = await LoginForm.PostAsync(client, Email, $"wrong-{i}");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            Assert.Contains("Invalid email or password.", await allowed.Content.ReadAsStringAsync());
        }

        // The next one is refused before the app ever checks the password.
        var refused = await LoginForm.PostAsync(client, Email, "wrong-4");
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        // And the right password is refused too, so there is no way past it.
        var correct = await LoginForm.PostAsync(client, Email, Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }
}

// ---- the failed-attempt lockout --------------------------------------------

public class LoginLockoutTests : IClassFixture<LoginLockoutTests.Factory>
{
    private const string Email = "lockout@test.local";
    private const string Password = "Test-Passw0rd!";

    public sealed class Factory : TestAppFactory
    {
        public Factory()
        {
            Settings["Security:Login:MaxFailedAttempts"] = "2";
            Settings["Security:Login:LockoutMinutes"] = "5";
            // Out of the way, so only the lockout can end this test.
            Settings["Security:Login:PermitLimit"] = "1000";
        }
    }

    private readonly Factory _factory;

    public LoginLockoutTests(Factory factory)
    {
        _factory = factory;
        Seed.User(factory, Email, Password);
    }

    [Fact] // failures are counted, and the account locks once they add up
    public async Task Login_LocksAccount_AfterFailedAttempts()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await LoginForm.PostAsync(client, Email, "wrong-1");
        await LoginForm.PostAsync(client, Email, "wrong-2");

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(Email);

        Assert.NotNull(user);
        Assert.True(await users.IsLockedOutAsync(user!), "the account should be locked out");

        // The correct password now gets nothing, and gives nothing away.
        var correct = await LoginForm.PostAsync(client, Email, Password);
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        Assert.Contains("Invalid email or password.", await correct.Content.ReadAsStringAsync());

        // No session was issued.
        var admin = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Found, admin.StatusCode);
    }
}

// ---- the shipped numbers ---------------------------------------------------

public class LoginSettingsTests : IClassFixture<LoginSettingsTests.Factory>
{
    // No overrides at all, so this reads the app's own appsettings.json and
    // catches a config section that stops parsing or stops binding.
    public sealed class Factory : TestAppFactory
    {
        public Factory() => Settings.Clear();
    }

    private readonly Factory _factory;

    public LoginSettingsTests(Factory factory) => _factory = factory;

    [Fact] // the controls run on the numbers we think they do
    public void ShippedConfig_BindsAndReachesIdentity()
    {
        using var scope = _factory.Services.CreateScope();

        var login = scope.ServiceProvider
            .GetRequiredService<IOptions<LoginSecurityOptions>>().Value;

        Assert.Equal(10, login.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(5), login.Window);
        Assert.Equal(10, login.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(1), login.LockoutDuration);

        // Identity has to be holding the same values, or the lockout is
        // configured in name only.
        var identity = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(identity.Lockout.AllowedForNewUsers);
        Assert.Equal(10, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(1), identity.Lockout.DefaultLockoutTimeSpan);
    }
}

internal static class Seed
{
    public static void User(TestAppFactory factory, string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (users.FindByEmailAsync(email).GetAwaiter().GetResult() is not null)
            return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = users.CreateAsync(user, password).GetAwaiter().GetResult();
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
    }
}
