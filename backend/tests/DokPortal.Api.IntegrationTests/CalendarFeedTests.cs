using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Prywatny adres kalendarza (iCal) do subskrypcji w Google Calendar: zawiera spotkania widoczne dla właściciela.</summary>
public class CalendarFeedTests : IntegrationTestBase
{
    public CalendarFeedTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private sealed record Feed(string? Url);

    private static string PathOf(string url) => new Uri(url).PathAndQuery;

    [Fact]
    public async Task TheFeed_ListsTheMeetingsTheOwnerMaySee_WithTimesInUtc()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, catechistId, _) = await SeedDokCaseAsync(admin, "Jan", "Kalendarzowy");
        var (otherCase, _, _) = await SeedDokCaseAsync(admin, "Obcy", "Podopieczny");
        (await admin.PostAsJsonAsync("/api/meetings", new { DokCaseId = caseId, MeetingDate = "2026-10-20", StartTime = "18:00", EndTime = "20:00", Notes = "Rozmowa; plan, cele" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/meetings", new { DokCaseId = otherCase, MeetingDate = "2026-10-21" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/meetings", new { GroupLabel = "Grupa całodniowa", MeetingDate = "2026-10-22", Attendees = new[] { new { DokCaseId = caseId } } })).EnsureSuccessStatusCode();
        var catechist = await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistId, "KatechistaProwadzacy");

        var none = await catechist.GetFromJsonAsync<Feed>("/api/calendar/feed");
        var feed = (await (await catechist.PostAsync("/api/calendar/feed", null)).Content.ReadFromJsonAsync<Feed>())!;
        var response = await Client.GetAsync(PathOf(feed.Url!));
        var ics = await response.Content.ReadAsStringAsync();

        Assert.Null(none!.Url);
        Assert.EndsWith(".ics", feed.Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.Contains("SUMMARY:Spotkanie: Jan Kalendarzowy", ics);
        Assert.Contains("DTSTART:20261020T160000Z", ics); // 18:00 czasu polskiego (CEST) = 16:00 UTC
        Assert.Contains("DTEND:20261020T180000Z", ics);
        Assert.Contains(@"DESCRIPTION:Rozmowa\; plan\, cele", ics);
        Assert.Contains("SUMMARY:Zajęcia: Grupa całodniowa", ics);
        Assert.Contains("DTSTART;VALUE=DATE:20261022", ics);
        Assert.DoesNotContain("Obcy Podopieczny", ics);
    }

    [Fact]
    public async Task ANewAddress_StopsTheOldOne_AndADisabledAccountGetsNothing()
    {
        var email = $"kal-{Guid.NewGuid():N}@example.org";
        var client = await CreateAuthenticatedClientAsync(email, "Sekret123!", "DyrektorDOK");
        var first = (await (await client.PostAsync("/api/calendar/feed", null)).Content.ReadFromJsonAsync<Feed>())!.Url!;
        var second = (await (await client.PostAsync("/api/calendar/feed", null)).Content.ReadFromJsonAsync<Feed>())!.Url!;

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(PathOf(first))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync(PathOf(second))).StatusCode);
        Assert.Equal(second, (await client.GetFromJsonAsync<Feed>("/api/calendar/feed"))!.Url);

        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var user = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);
        (await admin.PutAsJsonAsync($"/api/users/{user.Id}/active", new { IsActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(PathOf(second))).StatusCode);
    }

    [Fact]
    public async Task TheAddressCanBeTurnedOff_AndGuessingDoesNotWork()
    {
        var client = await CreateAuthenticatedClientAsync($"kal-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        var url = (await (await client.PostAsync("/api/calendar/feed", null)).Content.ReadFromJsonAsync<Feed>())!.Url!;

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/calendar/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync(PathOf(url))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/calendar/zgadywanie.ics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsync("/api/calendar/feed", null)).StatusCode);
    }

    [Fact]
    public async Task MeetingTimes_AreStoredAndValidated()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);

        var ok = await admin.PostAsJsonAsync("/api/meetings", new { DokCaseId = caseId, MeetingDate = "2026-10-20", StartTime = "18:00", EndTime = "20:00" });
        var body = await ok.Content.ReadAsStringAsync();
        var bad = await admin.PostAsJsonAsync("/api/meetings", new { DokCaseId = caseId, MeetingDate = "2026-10-20", StartTime = "20:00", EndTime = "18:00" });

        Assert.True(ok.IsSuccessStatusCode, body);
        Assert.Contains("\"startTime\":\"18:00:00\"", body);
        Assert.Contains("\"endTime\":\"20:00:00\"", body);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
