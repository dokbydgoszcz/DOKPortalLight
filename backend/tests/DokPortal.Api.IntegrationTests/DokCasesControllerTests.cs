using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DokCasesControllerTests : IntegrationTestBase
{
    public DokCasesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreatePersonAsync(HttpClient client, string firstName, string lastName)
    {
        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = firstName, LastName = lastName });
        response.EnsureSuccessStatusCode();
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        return person!.Id;
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedCase()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Jan", "Kowalski");
        var catechistId = await CreatePersonAsync(admin, "Anna", "Maj");

        var createResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = personId, Path = "Confirmation", Stage = "Evangelization", CatechistPersonId = catechistId
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Jan Kowalski", created!.PersonFullName);

        var getResponse = await admin.GetAsync($"/api/dok-cases/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Search_ByPath_ReturnsOnlyMatchingCases()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Karolina", "Szymanska");
        var catechistId = await CreatePersonAsync(admin, "Marek", "Zielinski");
        await admin.PostAsJsonAsync("/api/dok-cases", new { PersonId = personId, Path = "BaptismCandidate", Stage = "Prekatechumenate", CatechistPersonId = catechistId });

        var response = await admin.GetAsync("/api/dok-cases?path=BaptismCandidate");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DokCaseDto>>(EnumJsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result!.Items, c => c.PersonFullName == "Karolina Szymanska");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = Guid.NewGuid(), Path = "Confirmation", Stage = "Evangelization", CatechistPersonId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AStageThatDoesNotBelongToThePath_IsABadRequestNamingBoth()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Jan", "Kowalski");
        var catechistId = await SeedPersonAsync(admin, "Anna", "Maj");

        var invalid = await admin.PostAsJsonAsync("/api/dok-cases", new { PersonId = personId, Path = "Confirmation", Stage = "Election", CatechistPersonId = catechistId });
        var valid = await admin.PostAsJsonAsync("/api/dok-cases", new { PersonId = personId, Path = "BaptismCandidate", Stage = "Election", CatechistPersonId = catechistId });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        var message = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Wybranie", message);
        Assert.Contains("Bierzmowanie", message);
        Assert.Equal(System.Net.HttpStatusCode.Created, valid.StatusCode);
    }
}
