using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Zdjęcie profilowe na karcie osoby.</summary>
public class PersonPhotoTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public PersonPhotoTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7 };

    private static MultipartFormDataContent Image(byte[] bytes, string name = "zdjecie.png")
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(part, "file", name);
        return content;
    }

    [Fact]
    public async Task APhoto_IsUploadedReplacedShownAndRemoved()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Ewa", "Zdjęcie");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/people/{personId}/photo")).StatusCode);
        var upload = await admin.PutAsync($"/api/people/{personId}/photo", Image(Png));
        var dto = await upload.Content.ReadFromJsonAsync<PersonDto>();
        var png = await admin.GetAsync($"/api/people/{personId}/photo");
        (await admin.PutAsync($"/api/people/{personId}/photo", Image(Jpg, "a.jpg"))).EnsureSuccessStatusCode();
        var jpg = await admin.GetAsync($"/api/people/{personId}/photo");
        var delete = await admin.DeleteAsync($"/api/people/{personId}/photo");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.NotNull(dto!.PhotoUpdatedAtUtc);
        Assert.Equal("image/png", png.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await png.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", jpg.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/people/{personId}/photo")).StatusCode);
        Assert.Null((await admin.GetFromJsonAsync<PersonDto>($"/api/people/{personId}"))!.PhotoUpdatedAtUtc);
    }

    [Fact]
    public async Task OnlyImages_AndOnlyThoseWhoManagePeople()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Jan", "Plik");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var pdf = await admin.PutAsync($"/api/people/{personId}/photo", Image("%PDF-1.4"u8.ToArray(), "skan.png"));
        var forbidden = await catechist.PutAsync($"/api/people/{personId}/photo", Image(Png));
        var missing = await admin.PutAsync($"/api/people/{Guid.NewGuid()}/photo", Image(Png));

        Assert.Equal(HttpStatusCode.BadRequest, pdf.StatusCode);
        Assert.Contains("JPG, PNG lub WEBP", await pdf.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/people/{personId}/photo")).StatusCode);
    }
}
