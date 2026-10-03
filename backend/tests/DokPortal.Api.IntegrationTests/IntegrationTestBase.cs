using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DokPortal.Application.Auth;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    /// <summary>
    /// The server serializes enum-typed DTO properties as strings (global JsonStringEnumConverter
    /// in Program.cs), but HttpContent.ReadFromJsonAsync's own default options do not include that
    /// converter even under JsonSerializerDefaults.Web. Any test deserializing a DTO with an enum
    /// property (DokCaseDto.Path, SupervisionDto.Institution, ...) must pass this explicitly.
    /// </summary>
    protected static readonly JsonSerializerOptions EnumJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected readonly CustomWebApplicationFactory Factory;
    protected HttpClient Client = null!;

    protected IntegrationTestBase(CustomWebApplicationFactory factory) => Factory = factory;

    public Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<string> CreateUserAndGetTokenAsync(string email, string password, params string[] roles) =>
        CreateUserAndGetTokenAsync(email, password, null, roles);

    protected async Task<string> CreateUserAndGetTokenAsync(string email, string password, Guid? personId, params string[] roles)
    {
        using var scope = Factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, PersonId = personId };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(";", createResult.Errors.Select(e => e.Description)));
        }
        if (roles.Length > 0)
        {
            await userManager.AddToRolesAsync(user, roles);
        }

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password, params string[] roles)
    {
        var token = await CreateUserAndGetTokenAsync(email, password, roles);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Klient zalogowany na konto powiązane z osobą (np. katechistą prowadzącym sprawy).</summary>
    protected async Task<HttpClient> CreateAuthenticatedClientForPersonAsync(string email, string password, Guid personId, params string[] roles)
    {
        var token = await CreateUserAndGetTokenAsync(email, password, personId, roles);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected static async Task<Guid> SeedPersonAsync(HttpClient admin, string firstName, string lastName)
    {
        var response = await admin.PostAsJsonAsync("/api/people", new { FirstName = firstName, LastName = lastName });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PersonDto>())!.Id;
    }

    /// <summary>Tworzy osobę-podopiecznego, osobę-katechistę i sprawę DOK między nimi.</summary>
    protected async Task<(Guid CaseId, Guid CatechistPersonId, Guid StudentPersonId)> SeedDokCaseAsync(
        HttpClient admin, string studentFirst = "Jan", string studentLast = "Kowalski", string catechistFirst = "Anna", string catechistLast = "Maj")
    {
        var studentId = await SeedPersonAsync(admin, studentFirst, studentLast);
        var catechistId = await SeedPersonAsync(admin, catechistFirst, catechistLast);
        var response = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = studentId, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechistId
        });
        response.EnsureSuccessStatusCode();
        var dokCase = await response.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);
        return (dokCase!.Id, catechistId, studentId);
    }
}
