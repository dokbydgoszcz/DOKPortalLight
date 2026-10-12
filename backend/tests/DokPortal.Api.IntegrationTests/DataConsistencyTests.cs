using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Missions;
using DokPortal.Application.ParishNeeds;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
using DokPortal.Domain.Enums;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>
/// Spójność danych: osoby i parafii z zależnościami nie da się usunąć, a skierowanie ↔ misja ↔ funkcja Katechista
/// trzymają się razem (przegląd procesu 2026-10-08, A3–A5).
/// </summary>
public class DataConsistencyTests : IntegrationTestBase
{
    public DataConsistencyTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}".Substring(0, prefix.Length + 9);

    private static async Task<ParishDto> SeedParishAsync(HttpClient admin, string? name = null) =>
        (await (await admin.PostAsJsonAsync("/api/parishes", new { Name = name ?? Unique("Parafia") })).Content.ReadFromJsonAsync<ParishDto>())!;

    private static async Task<Guid> SeedNeedAsync(HttpClient admin, Guid parishId) =>
        (await (await admin.PostAsJsonAsync("/api/parish-needs", new { ParishId = parishId, Description = "Katecheza" }))
            .Content.ReadFromJsonAsync<ParishNeedDto>())!.Id;

    private static async Task<List<MissionDto>> MissionsOfAsync(HttpClient admin, Guid personId)
    {
        var page = await admin.GetFromJsonAsync<Application.Common.PagedResult<MissionDto>>("/api/missions?pageSize=500");
        return page!.Items.Where(m => m.PersonId == personId).ToList();
    }

    private static async Task<bool> IsCatechistAsync(HttpClient admin, Guid personId)
    {
        var person = await admin.GetFromJsonAsync<PersonDto>($"/api/people/{personId}", EnumJsonOptions);
        return person!.Functions.Any(f => f.Type == FunctionType.Catechist);
    }

    private static async Task<MissionDto> CreateMissionAsync(HttpClient admin, Guid personId, DateOnly start, DateOnly end, string place = "Parafia X")
    {
        var response = await admin.PostAsJsonAsync("/api/missions", new
        {
            PersonId = personId, ServicePlace = place, MissionStartDate = start, MissionEndDate = end
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MissionDto>())!;
    }

    // ---------- Usuwanie osoby ----------

    [Fact]
    public async Task DeletePerson_WithoutDependencies_Succeeds()
    {
        var admin = await AdminAsync();
        var personId = await SeedPersonAsync(admin, "Jan", "Samotny");

        var response = await admin.DeleteAsync($"/api/people/{personId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeletePerson_WhoIsAStudentOrCatechistOfADokCase_IsBlockedWithAReason()
    {
        var admin = await AdminAsync();
        var (_, catechistId, studentId) = await SeedDokCaseAsync(admin);

        var student = await admin.DeleteAsync($"/api/people/{studentId}");
        var catechist = await admin.DeleteAsync($"/api/people/{catechistId}");

        Assert.Equal(HttpStatusCode.BadRequest, student.StatusCode);
        Assert.Contains("sprawa DOK", await student.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, catechist.StatusCode);
        Assert.Contains("prowadzi", await catechist.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/people/{studentId}")).StatusCode);
    }

    [Fact]
    public async Task DeletePerson_WithAMission_IsBlocked()
    {
        var admin = await AdminAsync();
        var personId = await SeedPersonAsync(admin, "Ewa", "Misyjna");
        await CreateMissionAsync(admin, personId, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        var response = await admin.DeleteAsync($"/api/people/{personId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("misj", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeletePerson_WithACandidacy_IsBlocked()
    {
        var admin = await AdminAsync();
        var personId = await SeedPersonAsync(admin, "Piotr", "Kandydat");
        (await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 1, OpinionsCollected = 0 })).EnsureSuccessStatusCode();

        var response = await admin.DeleteAsync($"/api/people/{personId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("kandydat", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeletePerson_WithALinkedAccount_IsBlocked()
    {
        var admin = await AdminAsync();
        var personId = await SeedPersonAsync(admin, "Konto", "Powiazane");
        await CreateUserAndGetTokenAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", personId, "KatechistaProwadzacy");

        var response = await admin.DeleteAsync($"/api/people/{personId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("konto", await response.Content.ReadAsStringAsync());
    }

    // ---------- Usuwanie parafii ----------

    [Fact]
    public async Task DeleteParish_Empty_Succeeds_ButWithPeopleOrAPastorOrNeeds_IsBlocked()
    {
        var admin = await AdminAsync();
        var empty = await SeedParishAsync(admin);
        var withPerson = await SeedParishAsync(admin);
        var withPastor = await SeedParishAsync(admin);
        var withNeed = await SeedParishAsync(admin);
        (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Ola", LastName = "Parafianka", ParishId = withPerson.Id })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Ks", LastName = "Proboszcz",
            Functions = new[] { new { Type = "Pastor", ParishId = withPastor.Id } }
        })).EnsureSuccessStatusCode();
        await SeedNeedAsync(admin, withNeed.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/parishes/{empty.Id}")).StatusCode);
        var person = await admin.DeleteAsync($"/api/parishes/{withPerson.Id}");
        var pastor = await admin.DeleteAsync($"/api/parishes/{withPastor.Id}");
        var need = await admin.DeleteAsync($"/api/parishes/{withNeed.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, person.StatusCode);
        Assert.Contains("osob", await person.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, pastor.StatusCode);
        Assert.Contains("proboszcz", await pastor.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, need.StatusCode);
        Assert.Contains("zapotrzebowa", await need.Content.ReadAsStringAsync());
    }

    // ---------- Skierowanie ↔ misja ↔ funkcja ----------

    [Fact]
    public async Task Unassign_RemovesTheMissionCreatedByTheAssignment_AndTheCatechistFunction()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Anna", "Skierowana");
        (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();
        Assert.Single(await MissionsOfAsync(admin, personId));
        Assert.True(await IsCatechistAsync(admin, personId));

        (await admin.DeleteAsync($"/api/parish-needs/{needId}/assign/{personId}")).EnsureSuccessStatusCode();

        Assert.Empty(await MissionsOfAsync(admin, personId));
        Assert.False(await IsCatechistAsync(admin, personId));
    }

    [Fact]
    public async Task Unassign_OfAnAssignmentThatFilledAGrantedMission_OnlyClearsThePlace()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Beata", "Poslana");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var granted = await CreateMissionAsync(admin, personId, today, today.AddYears(1), place: "");
        (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();
        Assert.Equal(parish.Name, (await MissionsOfAsync(admin, personId)).Single().ServicePlace);

        (await admin.DeleteAsync($"/api/parish-needs/{needId}/assign/{personId}")).EnsureSuccessStatusCode();

        var mission = (await MissionsOfAsync(admin, personId)).Single();
        Assert.Equal(granted.Id, mission.Id);
        Assert.Equal("", mission.ServicePlace);
        Assert.True(await IsCatechistAsync(admin, personId));
    }

    [Fact]
    public async Task Assign_DoesNotFillAnExpiredMissionWithoutAPlace()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Celina", "Wygasla");
        var expired = await CreateMissionAsync(admin, personId, new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1), place: "");

        (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();

        var missions = await MissionsOfAsync(admin, personId);
        Assert.Equal(2, missions.Count);
        Assert.Equal("", missions.Single(m => m.Id == expired.Id).ServicePlace);
        Assert.Equal(parish.Name, missions.Single(m => m.Id != expired.Id).ServicePlace);
    }

    [Fact]
    public async Task RenamingTheParish_RenamesItsMissions_AndReassignmentDoesNotDuplicateTheMission()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var firstNeed = await SeedNeedAsync(admin, parish.Id);
        var secondNeed = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Dorota", "Stala");
        (await admin.PutAsJsonAsync($"/api/parish-needs/{firstNeed}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/parishes/{parish.Id}", new { Name = "Nowa nazwa" })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/parish-needs/{secondNeed}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();

        var mission = Assert.Single(await MissionsOfAsync(admin, personId));
        Assert.Equal("Nowa nazwa", mission.ServicePlace);
    }

    [Fact]
    public async Task EditingTheServicePlaceByHand_UnlinksTheMissionFromTheParish()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Nina", "Reczna");
        (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();
        var mission = (await MissionsOfAsync(admin, personId)).Single();

        (await admin.PutAsJsonAsync($"/api/missions/{mission.Id}", new
        {
            PersonId = personId, ServicePlace = "Szkoła nr 5", MissionStartDate = mission.MissionStartDate, MissionEndDate = mission.MissionEndDate
        })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/parishes/{parish.Id}", new { Name = "Inna nazwa" })).EnsureSuccessStatusCode();

        Assert.Equal("Szkoła nr 5", (await MissionsOfAsync(admin, personId)).Single().ServicePlace);
    }

    [Fact]
    public async Task UpdateNeed_CannotMoveItToAnotherParish_WhilePeopleAreAssigned()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var other = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Ewa", "Przypisana");
        (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = personId })).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}", new { ParishId = other.Id, Description = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("odepnij", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CloseNeed_BlocksFurtherAssignments_AndReopenRestoresTheStatus()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var first = await SeedPersonAsync(admin, "Fryderyk", "Pierwszy");
        var second = await SeedPersonAsync(admin, "Grzegorz", "Drugi");
        (await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = first })).EnsureSuccessStatusCode();

        var closed = await (await admin.PostAsync($"/api/parish-needs/{needId}/close", null)).Content.ReadFromJsonAsync<ParishNeedDto>();
        var assign = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = second });
        var reopened = await (await admin.PostAsync($"/api/parish-needs/{needId}/reopen", null)).Content.ReadFromJsonAsync<ParishNeedDto>();

        Assert.Equal("Closed", closed!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
        Assert.Contains("zamknięte", await assign.Content.ReadAsStringAsync());
        Assert.Equal("Assigned", reopened!.Status);
    }

    [Fact]
    public async Task Assign_ACandidateStillInFormation_IsBlocked()
    {
        var admin = await AdminAsync();
        var parish = await SeedParishAsync(admin);
        var needId = await SeedNeedAsync(admin, parish.Id);
        var personId = await SeedPersonAsync(admin, "Henryk", "WFormacji");
        (await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 2, OpinionsCollected = 0 })).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/parish-needs/{needId}/assign", new { PersonId = personId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("formacj", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DeletingTheOnlyMission_RemovesTheCatechistFunction_ButNotWhenAnotherMissionRemains()
    {
        var admin = await AdminAsync();
        var single = await SeedPersonAsync(admin, "Irena", "Jedna");
        var twice = await SeedPersonAsync(admin, "Jerzy", "Dwie");
        var only = await CreateMissionAsync(admin, single, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));
        var a = await CreateMissionAsync(admin, twice, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1));
        await CreateMissionAsync(admin, twice, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        (await admin.DeleteAsync($"/api/missions/{only.Id}")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/missions/{a.Id}")).EnsureSuccessStatusCode();

        Assert.False(await IsCatechistAsync(admin, single));
        Assert.True(await IsCatechistAsync(admin, twice));
    }

    [Fact]
    public async Task MovingAMissionToAnotherPerson_TakesTheFunctionFromThePreviousOne()
    {
        var admin = await AdminAsync();
        var previous = await SeedPersonAsync(admin, "Karol", "Poprzedni");
        var next = await SeedPersonAsync(admin, "Lena", "Nastepna");
        var mission = await CreateMissionAsync(admin, previous, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        (await admin.PutAsJsonAsync($"/api/missions/{mission.Id}", new
        {
            PersonId = next, ServicePlace = mission.ServicePlace, MissionStartDate = mission.MissionStartDate, MissionEndDate = mission.MissionEndDate
        })).EnsureSuccessStatusCode();

        Assert.False(await IsCatechistAsync(admin, previous));
        Assert.True(await IsCatechistAsync(admin, next));
    }

    [Fact]
    public async Task RemovingTheCatechistFunction_WhileAMissionIsValid_IsBlocked()
    {
        var admin = await AdminAsync();
        var personId = await SeedPersonAsync(admin, "Marek", "Aktywny");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateMissionAsync(admin, personId, today.AddMonths(-1), today.AddMonths(6));

        var response = await admin.PutAsJsonAsync($"/api/people/{personId}", new
        {
            FirstName = "Marek", LastName = "Aktywny", Functions = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ważną misję", await response.Content.ReadAsStringAsync());
    }
}
