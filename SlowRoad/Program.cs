using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
// Both System.Net and HttpOverrides ship an IPNetwork; this is the one
// ForwardedHeadersOptions.KnownNetworks takes.
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SlowRoad.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SlowRoad.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Login hardening settings. Bound through DI rather than read off
// builder.Configuration, so that the values are resolved once every config
// source is in place -- including the ones a test host adds.
builder.Services.AddOptions<LoginSecurityOptions>().BindConfiguration("Security:Login");

// Register ASP.NET Core Identity (no built-in pages)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Failed-attempt lockout, backed by the AspNetUsers.AccessFailedCount and
// LockoutEnd columns the Identity migration already created. Registered after
// AddIdentity so it wins, and as a dependent configuration so it sees the
// bound options rather than a value captured at startup.
builder.Services.AddOptions<IdentityOptions>()
    .Configure<IOptions<LoginSecurityOptions>>((identity, login) =>
    {
        identity.Lockout.AllowedForNewUsers = true;
        identity.Lockout.MaxFailedAccessAttempts = login.Value.MaxFailedAttempts;
        identity.Lockout.DefaultLockoutTimeSpan = login.Value.LockoutDuration;
    });

// nginx on the host proxies to this app, so the connection's remote address is
// the proxy, not the visitor. Without this every request in the world shares
// one rate-limit bucket (and Request.IsHttps is wrong behind the TLS hop).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // The container publishes its port on 127.0.0.1 only, so the one hop we
    // can ever see is nginx reaching us over the Docker bridge. Trusting the
    // private ranges it may appear on is therefore safe here; a header from
    // anywhere else cannot reach us.
    o.ForwardLimit = 1;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
    o.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
    o.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    o.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    o.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Per-IP cap on login attempts. This is the control that makes unlimited
// password guessing impossible, and it also keeps a flood of junk logins from
// burning the box's CPU on PBKDF2 hashing.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    o.AddPolicy("login", context =>
    {
        var login = context.RequestServices
            .GetRequiredService<IOptions<LoginSecurityOptions>>().Value;

        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = login.PermitLimit,
            Window = login.Window,
            QueueLimit = 0,
        });
    });

    o.OnRejected = async (ctx, token) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

        ctx.HttpContext.Response.ContentType = "text/plain";
        await ctx.HttpContext.Response.WriteAsync(
            "Too many login attempts. Try again later.", token);
    };
});

// Send users who aren't logged in to our login page
builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Admin/Login"; });

var app = builder.Build();

// apply DB migrations on startup (only when RunMigrations=true, e.g. in k8s)
if (app.Configuration["RunMigrations"] == "true")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await AdminSeeder.SeedAsync(app.Services, app.Configuration);

// Must run before anything that reads the client IP or the scheme.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// After UseRouting, so per-endpoint policies resolve.
app.UseRateLimiter();

// Enable auth middleware
app.UseAuthentication();
app.UseAuthorization();

// Serve static assets
app.MapStaticAssets();

// Default MVC route
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program
{
}
