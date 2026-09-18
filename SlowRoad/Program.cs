using Microsoft.EntityFrameworkCore;
using SlowRoad.Data;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Register ASP.NET Core Identity (no built-in pages)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Send users who aren't logged in to our login page
builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Admin/Login"; });

var app = builder.Build();
await AdminSeeder.SeedAsync(app.Services, app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();


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