using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.ParishNeeds;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ParishNeedsControllerTests : IntegrationTestBase
{
    public ParishNeedsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenAssign_UpdatesStatus()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var parishResponse = await admin.PostAsJsonAsync("/api/parishes", new { Name = "św. Pawła" });
        var parish = await parishResponse.Content.ReadFromJsonAsync<ParishDto>();
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Agnieszka", LastName = "Krol" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/parish-needs", new
        {
            ParishId = parish!.Id, Description = "Wsparcie katechezy dla dorosłych"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ParishNeedDto>();

        var assignResponse = await admin.PutAsJsonAsync($"/api/parish-needs/{created!.Id}/assign", new { PersonId = person!.Id });
        assignResponse.EnsureSuccessStatusCode();
        var assigned = await assignResponse.Content.ReadFromJsonAsync<ParishNeedDto>();

        Assert.Equal("Assigned", assigned!.Status);
        Assert.Equal(person.Id, assigned.AssignedPeople.Single().PersonId);
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/parish-needs", new { ParishId = Guid.NewGuid(), Description = "Test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(HttpClient Admin, Guid ParishId, Guid NeedId)> SeedNeedAsync()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var parish = await (await admin.PostAsJsonAsync("/api/parishes", new { Name = $"Parafia {Guid.NewGuid():N}".Substring(0, 14) })).Content.ReadFromJsonAsync<ParishDto>();
        var need = await (await admin.PostAsJsonAsync("/api/parish-needs", new { ParishId = parish!.Id, Description = "Katecheza" })).Content.ReadFromJsonAsync<ParishNeedDto>();
        return (admin, parish.Id, need!.Id);
    }

    [Fact]
    public async Task Assign_SeveralCatechists_ListsThemAll_AndUnassignRemovesOne()
    {
        var (admin, _, needId) = await SeedNeedAsync();
        var first = await SeedPersonAsync(admin, "Agnieszka", "Krol");
        var second = await SeedPersonAsync(admin, "Beata", "Lis");

        await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = first });
        var both = await (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = second })).Content.ReadFromJsonAsync<ParishNeedDto>();
        Assert.Equal(2, both!.AssignedPeople.Count);

        var unassign = await admin.DeleteAsync($"/api/parish-needs/{needId}/assign/{first}");
        var left = await unassign.Content.ReadFromJsonAsync<ParishNeedDto>();

        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);
        Assert.Equal(second, left!.AssignedPeople.Single().PersonId);
        Assert.Equal("Assigned", left.Status);
        var all = await admin.GetFromJsonAsync<List<ParishNeedDto>>("/api/parish-needs");
        Assert.Single(all!.Single(n => n.Id == needId).AssignedPeople);
    }

    [Fact]
    public async Task Update_ChangesTheNeed_AlsoAfterAssignment()
    {
        var (admin, parishId, needId) = await SeedNeedAsync();
        var person = await SeedPersonAsync(admin, "Agnieszka", "Krol");
        await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = person });

        var response = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}", new { ParishId = parishId, Description = "Dwóch katechistów" });
        var updated = await response.Content.ReadFromJsonAsync<ParishNeedDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Dwóch katechistów", updated!.Description);
        Assert.Equal("Assigned", updated.Status);
        Assert.Single(updated.AssignedPeople);
    }

    [Fact]
    public async Task Update_RejectsABlankDescription_UnknownParish_AndAnUnknownNeed()
    {
        var (admin, parishId, needId) = await SeedNeedAsync();

        var blank = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}", new { ParishId = parishId, Description = " " });
        var parish = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}", new { ParishId = Guid.NewGuid(), Description = "x" });
        var missing = await admin.PutAsJsonAsync($"/api/parish-needs/{Guid.NewGuid()}", new { ParishId = parishId, Description = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Contains("Podaj opis", await blank.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, parish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Assign_AnUnknownPerson_IsABadRequest_AndUnassignOfAnUnknownNeedIsNotFound()
    {
        var (admin, _, needId) = await SeedNeedAsync();

        var assign = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = Guid.NewGuid() });
        var unassign = await admin.DeleteAsync($"/api/parish-needs/{Guid.NewGuid()}/assign/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unassign.StatusCode);
    }

    [Fact]
    public async Task UpdateAndUnassign_WithoutTheManagePermission_AreForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var update = await client.PutAsJsonAsync($"/api/parish-needs/{Guid.NewGuid()}", new { ParishId = Guid.NewGuid(), Description = "x" });
        var unassign = await client.DeleteAsync($"/api/parish-needs/{Guid.NewGuid()}/assign/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unassign.StatusCode);
    }
}
