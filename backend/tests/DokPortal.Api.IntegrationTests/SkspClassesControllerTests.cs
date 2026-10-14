using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.SkspClasses;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SkspClassesControllerTests : IntegrationTestBase
{
    public SkspClassesControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task AdminCanCreateMarkAndDeleteAClass()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var created = await admin.PostAsJsonAsync("/api/sksp-classes", new { Year = 1, ClassDate = "2026-10-17", StartTime = "09:00:00", Topic = "Wprowadzenie" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = (await created.Content.ReadFromJsonAsync<SkspClassDto>())!;

        var list = await admin.GetFromJsonAsync<List<SkspClassDto>>("/api/sksp-classes?year=1");
        Assert.Contains(list!, c => c.Id == dto.Id);

        var edited = await admin.PutAsJsonAsync($"/api/sksp-classes/{dto.Id}", new { Year = 1, ClassDate = "2026-10-18", Topic = "Zmiana" });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/sksp-classes/{dto.Id}/attendance", Array.Empty<object>())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/sksp-classes/{dto.Id}/refresh-attendees", null)).StatusCode);
        var bad = await admin.PostAsJsonAsync("/api/sksp-classes", new { Year = 1, ClassDate = "2026-10-17", Topic = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/sksp-classes/{dto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/sksp-classes/{dto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/sksp-classes/{dto.Id}", new { Year = 1, ClassDate = "2026-10-18", Topic = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/sksp-classes/{dto.Id}/attendance", Array.Empty<object>())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/sksp-classes/{dto.Id}/refresh-attendees", null)).StatusCode);
    }

    [Fact]
    public async Task WithoutCandidatePermissions_ClassesAreForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/sksp-classes")).StatusCode);
    }
}
