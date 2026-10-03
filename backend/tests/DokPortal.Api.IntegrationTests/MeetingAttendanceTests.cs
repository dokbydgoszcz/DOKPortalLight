using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MeetingAttendanceTests : IntegrationTestBase
{
    public MeetingAttendanceTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    private static async Task<MeetingDto> CreateMeetingAsync(HttpClient client, Guid caseId)
    {
        var response = await client.PostAsJsonAsync("/api/meetings", new { DokCaseId = caseId, MeetingDate = Today });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MeetingDto>())!;
    }

    [Fact]
    public async Task Put_SetsAndClearsAttendance()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var meeting = await CreateMeetingAsync(admin, caseId);

        var present = await admin.PutAsJsonAsync($"/api/meetings/{meeting.Id}/attendance", new { IsAttended = true });
        var absent = await admin.PutAsJsonAsync($"/api/meetings/{meeting.Id}/attendance", new { IsAttended = false });
        var cleared = await admin.PutAsJsonAsync($"/api/meetings/{meeting.Id}/attendance", new { IsAttended = (bool?)null });

        Assert.True((await present.Content.ReadFromJsonAsync<MeetingDto>())!.IsAttended);
        Assert.False((await absent.Content.ReadFromJsonAsync<MeetingDto>())!.IsAttended);
        Assert.Null((await cleared.Content.ReadFromJsonAsync<MeetingDto>())!.IsAttended);
    }

    [Fact]
    public async Task Put_ForUnknownOrForeignMeeting_ReturnsNotFound()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var meeting = await CreateMeetingAsync(admin, caseId);
        var strangerPerson = await SeedPersonAsync(admin, "Beata", "Lis");
        var stranger = await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", strangerPerson, "KatechistaProwadzacy");

        var foreign = await stranger.PutAsJsonAsync($"/api/meetings/{meeting.Id}/attendance", new { IsAttended = true });
        var unknown = await admin.PutAsJsonAsync($"/api/meetings/{Guid.NewGuid()}/attendance", new { IsAttended = true });

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Put_RequiresManagePermission()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var meeting = await CreateMeetingAsync(admin, caseId);
        var superwizor = await CreateAuthenticatedClientAsync($"sup-{Guid.NewGuid():N}@example.org", "Sekret123!", "Superwizor");

        var response = await superwizor.PutAsJsonAsync($"/api/meetings/{meeting.Id}/attendance", new { IsAttended = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaseList_ReportsAttendanceSummary()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var marker = Guid.NewGuid().ToString("N")[..8];
        var (caseId, _, _) = await SeedDokCaseAsync(admin, "Jan", $"Frekwencja{marker}");
        foreach (var attended in new bool?[] { true, true, false, null })
        {
            var meeting = await CreateMeetingAsync(admin, caseId);
            await admin.PutAsJsonAsync($"/api/meetings/{meeting.Id}/attendance", new { IsAttended = attended });
        }

        var page = await admin.GetFromJsonAsync<PagedResult<DokCaseDto>>("/api/dok-cases?pageSize=100", EnumJsonOptions);

        var item = page!.Items.Single(i => i.Id == caseId);
        Assert.Equal((3, 2), (item.MeetingsRecorded, item.MeetingsAttended));
    }
}
