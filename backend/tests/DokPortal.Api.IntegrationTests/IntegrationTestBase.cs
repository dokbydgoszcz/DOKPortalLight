using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DokPortal.Application.Auth;
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

    protected async Task<string> CreateUserAndGetTokenAsync(string email, string password, params string[] roles)
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
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
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
}
