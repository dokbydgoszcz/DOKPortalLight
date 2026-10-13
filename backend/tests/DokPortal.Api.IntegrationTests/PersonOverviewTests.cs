using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Karta osoby: wszystkie powiązania w jednym miejscu, sekcje tylko dla ról z dostępem do danego ekranu.</summary>
public class PersonOverviewTests : IntegrationTestBase
{
    public PersonOverviewTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TheCard_GathersCasesMissionsCandidacyAndAccount()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, catechistId, studentId) = await SeedDokCaseAsync(admin, "Piotr", "Karta", "Ewa", "Prowadząca");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CanonicalMissions.Add(new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = catechistId, ServicePlace = "Fara", SupervisionGroup = "Grupa K",
                MissionStartDate = new DateOnly(2026, 1, 1), MissionEndDate = new DateOnly(2099, 1, 1), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
            db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = catechistId, FormationYear = 3, IsFormationCompleted = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var accountEmail = $"ewa-{Guid.NewGuid():N}@example.org";
        (await admin.PostAsJsonAsync("/api/users", new { Email = accountEmail, Password = "Sekret123!", PersonId = catechistId, Roles = new[] { "KatechistaProwadzacy" } }))
            .EnsureSuccessStatusCode();

        var catechist = await admin.GetFromJsonAsync<PersonOverviewDto>($"/api/people/{catechistId}/overview");
        var student = await admin.GetFromJsonAsync<PersonOverviewDto>($"/api/people/{studentId}/overview");

        Assert.Equal("Ewa Prowadząca", catechist!.Person.FullName);
        var led = Assert.Single(catechist.CasesLed!);
        Assert.Equal(("Piotr Karta", "katechista", caseId), (led.PersonFullName, led.Role, led.Id));
        Assert.Empty(catechist.DokCases!);
        Assert.Equal("Fara", Assert.Single(catechist.Missions!).ServicePlace);
        Assert.Equal("ukończył formację", catechist.Candidate!.Status);
        Assert.Equal(accountEmail, catechist.Account!.Email);
        Assert.Equal(new[] { "KatechistaProwadzacy" }, catechist.Account.Roles);
        Assert.Equal("Bierzmowanie", Assert.Single(student!.DokCases!).Path);
        Assert.Null(student.Account);
        Assert.True(student.AccountVisible);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/people/{Guid.NewGuid()}/overview")).StatusCode);
    }

    [Fact]
    public async Task SectionsAreHidden_WithoutAccessToTheirScreens()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (_, catechistId, _) = await SeedDokCaseAsync(admin);
        var role = $"Karta{Guid.NewGuid():N}"[..12];
        (await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = role })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/permissions/roles/{role}", new { Permissions = new[] { "People.View", "Missions.View" } })).EnsureSuccessStatusCode();
        var viewer = await CreateAuthenticatedClientAsync($"v-{Guid.NewGuid():N}@example.org", "Sekret123!", role);
        var noPeople = await CreateAuthenticatedClientAsync($"bp-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorSKSP");

        var card = await viewer.GetFromJsonAsync<PersonOverviewDto>($"/api/people/{catechistId}/overview");

        Assert.NotNull(card!.Missions);
        Assert.Null(card.DokCases);
        Assert.Null(card.CasesLed);
        Assert.False(card.CandidateVisible);
        Assert.Null(card.Documents);
        Assert.False(card.AccountVisible);
        Assert.Equal(HttpStatusCode.OK, (await noPeople.GetAsync($"/api/people/{catechistId}/overview")).StatusCode);
    }
}
