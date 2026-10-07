using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class GroupMeetingsTests : IntegrationTestBase
{
    public GroupMeetingsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    private sealed record World(HttpClient Admin, HttpClient CatechistA, HttpClient CatechistB, Guid CaseA1, Guid CaseA2, Guid CaseB);

    private async Task<World> SetUpAsync()
    {
        var marker = Guid.NewGuid().ToString("N").Substring(0, 8);
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseA1, catechistA, _) = await SeedDokCaseAsync(admin, "Jan", $"Uczestnik1-{marker}", "Anna", $"KatA{marker}");
        var studentA2 = await SeedPersonAsync(admin, "Ewa", $"Uczestnik2-{marker}");
        var caseA2Response = await admin.PostAsJsonAsync("/api/dok-cases", new { PersonId = studentA2, Path = "Confirmation", Stage = "Evangelization", CatechistPersonId = catechistA });
        var caseA2 = (await caseA2Response.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions))!.Id;
        var (caseB, catechistB, _) = await SeedDokCaseAsync(admin, "Piotr", $"Obcy-{marker}", "Beata", $"KatB{marker}");
        var clientA = await CreateAuthenticatedClientForPersonAsync($"kat-a-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistA, "KatechistaProwadzacy");
        var clientB = await CreateAuthenticatedClientForPersonAsync($"kat-b-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistB, "KatechistaProwadzacy");
        return new World(admin, clientA, clientB, caseA1, caseA2, caseB);
    }

    private static object Group(string label, params (Guid CaseId, bool? Attended)[] attendees) => new
    {
        GroupLabel = label,
        MeetingDate = Today,
        Attendees = attendees.Select(a => new { DokCaseId = a.CaseId, IsAttended = a.Attended }).ToArray()
    };

    [Fact]
    public async Task Catechist_CreatesGroupMeeting_AndSeesItWithAttendees()
    {
        var w = await SetUpAsync();

        var response = await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa wieczorna", (w.CaseA1, true), (w.CaseA2, false)));
        var created = await response.Content.ReadFromJsonAsync<MeetingDto>();
        var list = await w.CatechistA.GetFromJsonAsync<List<MeetingDto>>("/api/meetings");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, created!.Attendees.Count);
        var listed = list!.Single(m => m.Id == created.Id);
        Assert.True(listed.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.False(listed.Attendees.Single(a => a.DokCaseId == w.CaseA2).IsAttended);
    }

    [Fact]
    public async Task GroupMeeting_IsHiddenFromOtherCatechists_ButVisibleToAdmin()
    {
        var w = await SetUpAsync();
        var created = await (await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa A", (w.CaseA1, null))))
            .Content.ReadFromJsonAsync<MeetingDto>();

        var listB = await w.CatechistB.GetFromJsonAsync<List<MeetingDto>>("/api/meetings");
        var getB = await w.CatechistB.GetAsync($"/api/meetings/{created!.Id}");
        var listAdmin = await w.Admin.GetFromJsonAsync<List<MeetingDto>>("/api/meetings");

        Assert.DoesNotContain(listB!, m => m.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, getB.StatusCode);
        Assert.Contains(listAdmin!, m => m.Id == created.Id);
    }

    [Fact]
    public async Task Create_RejectsForeignAttendeesAndMixedMeetings()
    {
        var w = await SetUpAsync();

        var foreign = await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa", (w.CaseA1, null), (w.CaseB, null)));
        var mixed = await w.CatechistA.PostAsJsonAsync("/api/meetings", new
        {
            DokCaseId = w.CaseA1,
            MeetingDate = Today,
            Attendees = new[] { new { DokCaseId = w.CaseA2, IsAttended = (bool?)null } }
        });
        var duplicate = await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa", (w.CaseA1, null), (w.CaseA1, true)));

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task AttendeeToggle_SetsClearsAndRespectsScopeAndPermissions()
    {
        var w = await SetUpAsync();
        var created = await (await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa", (w.CaseA1, null), (w.CaseA2, null))))
            .Content.ReadFromJsonAsync<MeetingDto>();
        var superwizor = await CreateAuthenticatedClientAsync($"sup-{Guid.NewGuid():N}@example.org", "Sekret123!", "Superwizor");

        var present = await w.CatechistA.PutAsJsonAsync($"/api/meetings/{created!.Id}/attendees/{w.CaseA1}", new { IsAttended = true });
        var cleared = await w.CatechistA.PutAsJsonAsync($"/api/meetings/{created.Id}/attendees/{w.CaseA1}", new { IsAttended = (bool?)null });
        var notAttendee = await w.CatechistA.PutAsJsonAsync($"/api/meetings/{created.Id}/attendees/{w.CaseB}", new { IsAttended = true });
        var foreign = await w.CatechistB.PutAsJsonAsync($"/api/meetings/{created.Id}/attendees/{w.CaseA1}", new { IsAttended = true });
        var forbidden = await superwizor.PutAsJsonAsync($"/api/meetings/{created.Id}/attendees/{w.CaseA1}", new { IsAttended = true });

        Assert.True((await present.Content.ReadFromJsonAsync<MeetingDto>())!.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.Null((await cleared.Content.ReadFromJsonAsync<MeetingDto>())!.Attendees.Single(a => a.DokCaseId == w.CaseA1).IsAttended);
        Assert.Equal(HttpStatusCode.NotFound, notAttendee.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Update_ReplacesAttendees_AndGroupAttendanceFeedsTheCaseSummary()
    {
        var w = await SetUpAsync();
        var created = await (await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa", (w.CaseA1, true), (w.CaseA2, false))))
            .Content.ReadFromJsonAsync<MeetingDto>();

        var update = await w.CatechistA.PutAsJsonAsync($"/api/meetings/{created!.Id}", Group("Grupa po zmianie", (w.CaseA1, true)));
        var page = await w.CatechistA.GetFromJsonAsync<PagedResult<DokCaseDto>>("/api/dok-cases?pageSize=100", EnumJsonOptions);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Single((await update.Content.ReadFromJsonAsync<MeetingDto>())!.Attendees);
        var one = page!.Items.Single(i => i.Id == w.CaseA1);
        var two = page.Items.Single(i => i.Id == w.CaseA2);
        Assert.Equal((1, 1), (one.MeetingsRecorded, one.MeetingsAttended));
        Assert.Equal((0, 0), (two.MeetingsRecorded, two.MeetingsAttended));
    }

    [Fact]
    public async Task DeletingAGroupMeeting_RemovesItFromTheListAndTheSummary()
    {
        var w = await SetUpAsync();
        var created = await (await w.CatechistA.PostAsJsonAsync("/api/meetings", Group("Grupa", (w.CaseA1, true))))
            .Content.ReadFromJsonAsync<MeetingDto>();

        var delete = await w.CatechistA.DeleteAsync($"/api/meetings/{created!.Id}");
        var list = await w.CatechistA.GetFromJsonAsync<List<MeetingDto>>("/api/meetings");
        var page = await w.CatechistA.GetFromJsonAsync<PagedResult<DokCaseDto>>("/api/dok-cases?pageSize=100", EnumJsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(list!, m => m.Id == created.Id);
        Assert.Equal(0, page!.Items.Single(i => i.Id == w.CaseA1).MeetingsRecorded);
    }
}
