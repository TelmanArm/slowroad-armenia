using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using SlowRoad.Controllers;
using SlowRoad.Data;
using SlowRoad.Models;

namespace SlowRoad.Tests;

// The admin editor: creating, editing and deleting places, and the photo
// upload path (the one that stores bytes we later serve back to the public).
public class PlacesControllerTests
{
    private static AppDbContext NewDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static PlacesController NewController(AppDbContext db) =>
        new(db)
        {
            // AddPhotos and the reorder actions write user-facing messages here.
            TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider()),
        };

    private static IFormFile Upload(string name, byte[] bytes, string claimedType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photos", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = claimedType,
        };

    // ---- create / edit / delete -------------------------------------------

    [Fact] // a valid place is saved
    public async Task Create_SavesPlace()
    {
        using var db = NewDb(nameof(Create_SavesPlace));
        var result = await NewController(db).Create(new Place { Name = "Garni", Slug = "garni" });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Garni", db.Places.Single().Name);
    }

    [Fact] // an invalid place is not saved, and the form comes back
    public async Task Create_InvalidDoesNotSave()
    {
        using var db = NewDb(nameof(Create_InvalidDoesNotSave));
        var controller = NewController(db);
        controller.ModelState.AddModelError(nameof(Place.Name), "Required");

        var result = await controller.Create(new Place { Slug = "no-name" });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(db.Places);
    }

    [Fact] // editing updates the fields the form posts
    public async Task Edit_UpdatesFields()
    {
        using var db = NewDb(nameof(Edit_UpdatesFields));
        db.Places.Add(new Place { Name = "Old", Slug = "dilijan", Region = "Tavush" });
        await db.SaveChangesAsync();
        var id = db.Places.Single().Id;

        await NewController(db).Edit(id, new Place
        {
            Name = "New",
            Region = "Tavush",
            ShortDescription = "Forested spa town.",
            Latitude = 40.74,
        });

        var saved = db.Places.Single();
        Assert.Equal("New", saved.Name);
        Assert.Equal("Forested spa town.", saved.ShortDescription);
        Assert.Equal(40.74, saved.Latitude);
    }

    [Fact] // the slug is the image identifier: editing must never change it
    public async Task Edit_KeepsSlug()
    {
        using var db = NewDb(nameof(Edit_KeepsSlug));
        db.Places.Add(new Place { Name = "Dilijan", Slug = "dilijan" });
        await db.SaveChangesAsync();
        var id = db.Places.Single().Id;

        await NewController(db).Edit(id, new Place { Name = "Dilijan", Slug = "hacked" });

        Assert.Equal("dilijan", db.Places.Single().Slug);
    }

    [Fact] // editing an id that isn't there is a 404, not a crash
    public async Task Edit_UnknownId_NotFound()
    {
        using var db = NewDb(nameof(Edit_UnknownId_NotFound));
        Assert.IsType<NotFoundResult>(await NewController(db).Edit(999, new Place { Name = "X" }));
    }

    [Fact] // delete removes the place
    public async Task Delete_RemovesPlace()
    {
        using var db = NewDb(nameof(Delete_RemovesPlace));
        db.Places.Add(new Place { Name = "Gone", Slug = "gone" });
        await db.SaveChangesAsync();

        await NewController(db).DeleteConfirmed(db.Places.Single().Id);

        Assert.Empty(db.Places);
    }

    // ---- photo upload ------------------------------------------------------

    [Fact] // a real photo is stored once per served width, resized
    public async Task AddPhotos_StoresResizedWidths()
    {
        using var db = NewDb(nameof(AddPhotos_StoresResizedWidths));
        db.Places.Add(new Place { Name = "Garni", Slug = "garni" });
        await db.SaveChangesAsync();
        var id = db.Places.Single().Id;

        var bytes = ImageProcessorTests.MakeImage(3000, 2000);
        await NewController(db).AddPhotos(id, new List<IFormFile> { Upload("garni.jpg", bytes, "image/jpeg") });

        var files = db.PhotoFiles.OrderBy(f => f.Width).ToList();
        Assert.Equal(new[] { 700, 1400 }, files.Select(f => f.Width).ToArray());
        Assert.All(files, f => Assert.Equal("image/jpeg", f.ContentType));

        // Each row holds its own, smaller bytes - not a copy of the upload.
        Assert.All(files, f => Assert.True(f.Bytes.Length < bytes.Length));
        Assert.NotEqual(files[0].Bytes.Length, files[1].Bytes.Length);
    }

    [Fact] // an SVG claiming to be a PNG is rejected: this is the stored-XSS case
    public async Task AddPhotos_RejectsSvgClaimingToBeImage()
    {
        using var db = NewDb(nameof(AddPhotos_RejectsSvgClaimingToBeImage));
        db.Places.Add(new Place { Name = "Garni", Slug = "garni" });
        await db.SaveChangesAsync();
        var id = db.Places.Single().Id;

        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(document.cookie)</script></svg>");
        var controller = NewController(db);

        // The client lies twice: file extension and Content-Type.
        await controller.AddPhotos(id, new List<IFormFile> { Upload("evil.png", svg, "image/png") });

        Assert.Empty(db.Photos);
        Assert.Empty(db.PhotoFiles);
        Assert.Contains("evil.png", controller.TempData["PhotoError"]?.ToString());
    }

    [Fact] // a good photo alongside a bad one: the good one still lands
    public async Task AddPhotos_SkipsOnlyTheBadFile()
    {
        using var db = NewDb(nameof(AddPhotos_SkipsOnlyTheBadFile));
        db.Places.Add(new Place { Name = "Garni", Slug = "garni" });
        await db.SaveChangesAsync();
        var id = db.Places.Single().Id;

        var controller = NewController(db);
        await controller.AddPhotos(id, new List<IFormFile>
        {
            Upload("good.png", ImageProcessorTests.MakeImage(2000, 1500, SKEncodedImageFormat.Png), "image/png"),
            Upload("bad.txt", System.Text.Encoding.UTF8.GetBytes("nope"), "image/png"),
        });

        Assert.Single(db.Photos);
        Assert.Contains("bad.txt", controller.TempData["PhotoError"]?.ToString());
        Assert.All(db.PhotoFiles, f => Assert.Equal("image/png", f.ContentType));
    }

    [Fact] // uploads land after the photos already there, in order
    public async Task AddPhotos_AppendsInOrder()
    {
        using var db = NewDb(nameof(AddPhotos_AppendsInOrder));
        var place = new Place { Name = "Garni", Slug = "garni" };
        place.Photos.Add(new Photo { Alt = "First", SortOrder = 0 });
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var bytes = ImageProcessorTests.MakeImage(1000, 800);
        await NewController(db).AddPhotos(place.Id, new List<IFormFile> { Upload("b.jpg", bytes, "image/jpeg") });

        var photos = db.Photos.OrderBy(p => p.SortOrder).ToList();
        Assert.Equal(2, photos.Count);
        Assert.Equal(new[] { 0, 1 }, photos.Select(p => p.SortOrder).ToArray());
    }

    [Fact] // uploading to a place that isn't there is a 404
    public async Task AddPhotos_UnknownPlace_NotFound()
    {
        using var db = NewDb(nameof(AddPhotos_UnknownPlace_NotFound));
        var bytes = ImageProcessorTests.MakeImage(800, 600);

        var result = await NewController(db)
            .AddPhotos(999, new List<IFormFile> { Upload("x.jpg", bytes, "image/jpeg") });

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- photo ordering ----------------------------------------------------

    [Fact] // the chosen photo moves to the front and the rest renumber from 0
    public async Task MakeMainPhoto_MovesToFront()
    {
        using var db = NewDb(nameof(MakeMainPhoto_MovesToFront));
        var place = new Place { Name = "Garni", Slug = "garni" };
        place.Photos.Add(new Photo { Alt = "A", SortOrder = 0 });
        place.Photos.Add(new Photo { Alt = "B", SortOrder = 1 });
        place.Photos.Add(new Photo { Alt = "C", SortOrder = 2 });
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var b = db.Photos.Single(p => p.Alt == "B");
        await NewController(db).MakeMainPhoto(place.Id, b.Id);

        Assert.Equal(new[] { "B", "A", "C" },
            db.Photos.OrderBy(p => p.SortOrder).Select(p => p.Alt).ToArray());
    }

    [Fact] // deleting a photo takes its stored files with it and closes the gap
    public async Task DeletePhoto_RemovesFilesAndRenumbers()
    {
        using var db = NewDb(nameof(DeletePhoto_RemovesFilesAndRenumbers));
        db.Places.Add(new Place { Name = "Garni", Slug = "garni" });
        await db.SaveChangesAsync();
        var id = db.Places.Single().Id;

        var controller = NewController(db);
        await controller.AddPhotos(id, new List<IFormFile>
        {
            Upload("a.jpg", ImageProcessorTests.MakeImage(2000, 1500), "image/jpeg"),
            Upload("b.jpg", ImageProcessorTests.MakeImage(1800, 1200), "image/jpeg"),
        });
        Assert.Equal(2, db.Photos.Count());

        var first = db.Photos.OrderBy(p => p.SortOrder).First();
        await NewController(db).DeletePhoto(id, first.Id);

        Assert.Single(db.Photos);
        Assert.Equal(0, db.Photos.Single().SortOrder);
        // In-memory EF has no cascade delete, so assert the rows that remain
        // all belong to the photo that is still there.
        var keptId = db.Photos.Single().Id;
        Assert.All(db.PhotoFiles.Where(f => f.PhotoId != keptId),
            f => Assert.Equal(keptId, f.PhotoId));
    }

    [Fact] // reordering past the end stays put instead of throwing
    public async Task MovePhoto_ClampsAtTheEdges()
    {
        using var db = NewDb(nameof(MovePhoto_ClampsAtTheEdges));
        var place = new Place { Name = "Garni", Slug = "garni" };
        place.Photos.Add(new Photo { Alt = "A", SortOrder = 0 });
        place.Photos.Add(new Photo { Alt = "B", SortOrder = 1 });
        db.Places.Add(place);
        await db.SaveChangesAsync();

        var a = db.Photos.Single(p => p.Alt == "A");
        await NewController(db).MovePhoto(place.Id, a.Id, -1);   // already first

        Assert.Equal(new[] { "A", "B" },
            db.Photos.OrderBy(p => p.SortOrder).Select(p => p.Alt).ToArray());
    }

    // TempData needs a provider; nothing here reads it back across requests.
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
