using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Reminders;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class RemindersControllerTests : IntegrationTestBase
{
    public RemindersControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Run_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/api/reminders/missing-documents/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/missing-documents/run");
        request.Headers.Add("X-Reminders-Key", "zly-klucz");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_WithCorrectKey_ReturnsOkWithResult()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/missing-documents/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MissingDocumentsReminderResultDto>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RunUpcomingMeetings_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/api/reminders/upcoming-meetings/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingMeetings_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-meetings/run");
        request.Headers.Add("X-Reminders-Key", "zly-klucz");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingMeetings_WithCorrectKey_ReturnsOkWithResult()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-meetings/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpcomingMeetingsReminderResultDto>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RunUpcomingNameDays_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/api/reminders/upcoming-name-days/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingNameDays_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-name-days/run");
        request.Headers.Add("X-Reminders-Key", "zly-klucz");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingNameDays_WithCorrectKey_ReturnsOkWithResult()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-name-days/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpcomingNameDaysReminderResultDto>();
        Assert.NotNull(result);
    }
}
