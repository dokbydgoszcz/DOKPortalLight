using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
using DokPortal.Domain.Enums;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Funkcje osób przez API: zapis z listą funkcji, filtr listy, jeden proboszcz na parafię.</summary>
public class PersonFunctionEndpointsTests : IntegrationTestBase
{
    public PersonFunctionEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<HttpClient> AdminAsync() =>
        await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private static async Task<Guid> SeedParishAsync(HttpClient admin, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/parishes", new { Name = name, City = "Bydgoszcz" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ParishDto>())!.Id;
    }

    [Fact]
    public async Task APersonSavedWithFunctions_ShowsUpInTheFilteredList()
    {
        var admin = await AdminAsync();
        var last = $"Akolita{Guid.NewGuid():N}".Substring(0, 16);

        var created = await admin.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Jan", LastName = last,
            Functions = new[] { new { Type = "Acolyte", Notes = "Ustanowiony w katedrze" } }
        });
        var found = await admin.GetFromJsonAsync<PagedResult<PersonDto>>($"/api/people?query={last}&function=Acolyte", EnumJsonOptions);
        var otherFunction = await admin.GetFromJsonAsync<PagedResult<PersonDto>>($"/api/people?query={last}&function=Lector", EnumJsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var person = Assert.Single(found!.Items);
        Assert.Equal("Ustanowiony w katedrze", Assert.Single(person.Functions).Notes);
        Assert.Empty(otherFunction!.Items);
    }

    [Fact]
    public async Task AParishWithAPastor_RefusesASecondOne_WithAMessageNamingHim()
    {
        var admin = await AdminAsync();
        var parishId = await SeedParishAsync(admin, $"św. Jana {Guid.NewGuid():N}".Substring(0, 20));
        var first = await admin.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Adam", LastName = "Pierwszy", Functions = new[] { new { Type = "Pastor", ParishId = parishId } }
        });

        var second = await admin.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Piotr", LastName = "Drugi", Functions = new[] { new { Type = "Pastor", ParishId = parishId } }
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("Adam Pierwszy", await second.Content.ReadAsStringAsync());
    }
}
