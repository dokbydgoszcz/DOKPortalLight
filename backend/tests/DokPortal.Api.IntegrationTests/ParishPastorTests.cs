using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Proboszcz przypisywany od strony parafii (w parze z funkcją Proboszcz przy osobie).</summary>
public class ParishPastorTests : IntegrationTestBase
{
    public ParishPastorTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task APastor_ChosenOnTheParish_GetsThePastorFunction_AndTheOldOneIsReleased()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var first = await SeedPersonAsync(admin, "Jan", "Proboszcz");
        var second = await SeedPersonAsync(admin, "Adam", "Następca");

        var created = await (await admin.PostAsJsonAsync("/api/parishes", new { Name = $"Parafia {Guid.NewGuid():N}"[..20], City = "Bydgoszcz", PastorPersonId = first }))
            .Content.ReadFromJsonAsync<ParishDto>();
        Assert.Equal(first, created!.PastorPersonId);
        Assert.Equal("Jan Proboszcz", created.PastorFullName);
        var firstPerson = await admin.GetFromJsonAsync<PersonDto>($"/api/people/{first}", EnumJsonOptions);
        Assert.Contains(firstPerson!.Functions, f => f.Type.ToString() == "Pastor" && f.ParishId == created.Id);

        var update = await admin.PutAsJsonAsync($"/api/parishes/{created.Id}", new { Name = created.Name, City = "Bydgoszcz", PastorPersonId = second });
        var updated = await update.Content.ReadFromJsonAsync<ParishDto>();
        Assert.Equal(second, updated!.PastorPersonId);
        var released = await admin.GetFromJsonAsync<PersonDto>($"/api/people/{first}", EnumJsonOptions);
        Assert.Contains(released!.Functions, f => f.Type.ToString() == "Pastor" && f.ParishId == null);

        var cleared = await (await admin.PutAsJsonAsync($"/api/parishes/{created.Id}", new { Name = created.Name, City = "Bydgoszcz" }))
            .Content.ReadFromJsonAsync<ParishDto>();
        Assert.Null(cleared!.PastorPersonId);
        var list = await admin.GetFromJsonAsync<List<ParishDto>>("/api/parishes");
        Assert.Null(list!.Single(p => p.Id == created.Id).PastorFullName);
    }

    [Fact]
    public async Task AnUnknownPerson_IsRefused()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var response = await admin.PostAsJsonAsync("/api/parishes", new { Name = "Parafia X", PastorPersonId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
