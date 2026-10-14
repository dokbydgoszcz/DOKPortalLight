using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Mailing: poprawianie szkicu, podgląd odbiorców, grupy proboszczów/formatorów/własna lista i załączniki.</summary>
public class MailingWorkflowTests : IntegrationTestBase, IClassFixture<MailingFactory>
{
    private readonly MailingFactory _factory;

    public MailingWorkflowTests(CustomWebApplicationFactory unused, MailingFactory factory) : base(factory) => _factory = factory;

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private async Task<Person> AddPersonAsync(string last, string? email, Action<AppDbContext, Person>? extra = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        extra?.Invoke(db, person);
        await db.SaveChangesAsync();
        return person;
    }

    private static async Task<MailingCampaignDto> CreateAsync(HttpClient admin, object body)
    {
        var response = await admin.PostAsJsonAsync("/api/mailing/campaigns", body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions))!;
    }

    [Fact]
    public async Task ADraft_CanBeCorrected_ButASentCampaignCannot()
    {
        var admin = await AdminAsync();
        var a = await AddPersonAsync("Adresat", $"a-{Guid.NewGuid():N}@example.org");
        var draft = await CreateAsync(admin, new { Subject = "Temat", Body = "Treść", Group = "Custom", PersonIds = new[] { a.Id } });

        var edit = await admin.PutAsJsonAsync($"/api/mailing/campaigns/{draft.Id}", new { Subject = "Nowy temat", Body = "Nowa treść", Group = "Custom", PersonIds = new[] { a.Id } });
        var edited = await edit.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions);
        (await admin.PostAsync($"/api/mailing/campaigns/{draft.Id}/send", null)).EnsureSuccessStatusCode();
        var afterSend = await admin.PutAsJsonAsync($"/api/mailing/campaigns/{draft.Id}", new { Subject = "x", Body = "y", Group = "Custom", PersonIds = new[] { a.Id } });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("Nowy temat", edited!.Subject);
        Assert.Equal("Nowa treść", edited.Body);
        Assert.Equal(HttpStatusCode.BadRequest, afterSend.StatusCode);
        Assert.Contains(_factory.Sender.Sent, m => m.To == a.Email && m.Subject == "Nowy temat");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/mailing/campaigns/{Guid.NewGuid()}", new { Subject = "x", Body = "y", Group = "DokCases" })).StatusCode);
    }

    [Fact]
    public async Task ACustomList_GoesOnlyToThePickedPeople_AndNeedsAtLeastOne()
    {
        var admin = await AdminAsync();
        var a = await AddPersonAsync("Pierwszy", $"p1-{Guid.NewGuid():N}@example.org");
        var b = await AddPersonAsync("Drugi", $"p2-{Guid.NewGuid():N}@example.org");
        var noEmail = await AddPersonAsync("BezMaila", null);
        var outsider = await AddPersonAsync("Obcy", $"obcy-{Guid.NewGuid():N}@example.org");

        var empty = await admin.PostAsJsonAsync("/api/mailing/campaigns", new { Subject = "T", Body = "B", Group = "Custom", PersonIds = Array.Empty<Guid>() });
        var campaign = await CreateAsync(admin, new { Subject = "Lista", Body = "B", Group = "Custom", PersonIds = new[] { a.Id, b.Id, noEmail.Id } });
        var recipients = await admin.GetFromJsonAsync<List<MailingRecipientDto>>($"/api/mailing/campaigns/{campaign.Id}/recipients");
        (await admin.PostAsync($"/api/mailing/campaigns/{campaign.Id}/send", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(3, campaign.RecipientCount);
        Assert.Equal(new[] { a.Id, b.Id, noEmail.Id }.OrderBy(x => x), campaign.PersonIds.OrderBy(x => x));
        Assert.Equal(3, recipients!.Count);
        Assert.Null(recipients.Single(r => r.PersonId == noEmail.Id).Email);
        var sent = _factory.Sender.Sent.Where(m => m.Subject == "Lista").Select(m => m.To).ToList();
        Assert.Equal(new[] { a.Email, b.Email }.OrderBy(x => x), sent.OrderBy(x => x));
        Assert.DoesNotContain(outsider.Email, sent);
    }

    [Fact]
    public async Task PastorsAndFormators_AreGroups_AndTheRecipientsCanBePreviewedBeforeSaving()
    {
        var admin = await AdminAsync();
        var pastor = await AddPersonAsync("Proboszcz", $"pb-{Guid.NewGuid():N}@example.org",
            (db, p) => db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = p.Id, Type = FunctionType.Pastor }));
        var formator = await AddPersonAsync("Formator", null,
            (db, p) => db.Formators.Add(new Formator { Id = Guid.NewGuid(), PersonId = p.Id, Function = "Wykładowca" }));

        var pastors = await admin.GetFromJsonAsync<List<MailingRecipientDto>>("/api/mailing/recipients?group=Pastors");
        var formators = await admin.GetFromJsonAsync<List<MailingRecipientDto>>("/api/mailing/recipients?group=Formators");
        var bishop = await CreateAuthenticatedClientAsync($"bp-{Guid.NewGuid():N}@example.org", "Sekret123!", "Biskup");

        Assert.Contains(pastors!, r => r.PersonId == pastor.Id && r.Email == pastor.Email && r.FullName == "Jan Proboszcz");
        var f = Assert.Single(formators!, r => r.PersonId == formator.Id);
        Assert.Null(f.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.GetAsync("/api/mailing/recipients?group=Pastors")).StatusCode);
    }

    [Fact]
    public async Task AFileAttachedToTheDraft_IsSentWithEveryMessage_AndIsFrozenAfterSending()
    {
        var admin = await AdminAsync();
        var a = await AddPersonAsync("Zalacznik", $"z-{Guid.NewGuid():N}@example.org");
        var campaign = await CreateAsync(admin, new { Subject = "Z plikiem", Body = "B", Group = "Custom", PersonIds = new[] { a.Id } });
        MultipartFormDataContent File()
        {
            var content = new MultipartFormDataContent();
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes("PLAN-REKOLEKCJI"));
            part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(part, "file", "plan.pdf");
            return content;
        }

        var upload = await admin.PostAsync($"/api/mailing/campaigns/{campaign.Id}/attachments", File());
        var listed = (await admin.GetFromJsonAsync<List<MailingCampaignDto>>("/api/mailing/campaigns", EnumJsonOptions))!.Single(c => c.Id == campaign.Id);
        (await admin.PostAsync($"/api/mailing/campaigns/{campaign.Id}/send", null)).EnsureSuccessStatusCode();
        var late = await admin.PostAsync($"/api/mailing/campaigns/{campaign.Id}/attachments", File());

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal("plan.pdf", Assert.Single(listed.Attachments).FileName);
        var mail = Assert.Single(_factory.Sender.Sent, m => m.Subject == "Z plikiem");
        Assert.Equal(new[] { "plan.pdf:15" }, mail.Files);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Contains("wysłanej", await late.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SendingQueuesTheCampaign_TheRestGoesOutInTheBackground_AndTheFooterLetsPeopleUnsubscribe()
    {
        var admin = await AdminAsync();
        var a = await AddPersonAsync("Kolejka1", $"k1-{Guid.NewGuid():N}@example.org");
        var b = await AddPersonAsync("Kolejka2", $"k2-{Guid.NewGuid():N}@example.org");
        var campaign = await CreateAsync(admin, new { Subject = "Kolejka", Body = "Treść kolejki", Group = "Custom", PersonIds = new[] { a.Id, b.Id } });

        var sent = await (await admin.PostAsync($"/api/mailing/campaigns/{campaign.Id}/send", null)).Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions);
        Assert.Equal(CampaignStatus.Sending, sent!.Status);
        var listed = (await admin.GetFromJsonAsync<List<MailingCampaignDto>>("/api/mailing/campaigns", EnumJsonOptions))!.Single(c => c.Id == campaign.Id);
        Assert.Equal((CampaignStatus.Sent, 2, 0), (listed.Status, listed.RecipientCount, listed.PendingCount));

        var toA = _factory.Sender.Sent.Single(m => m.To == a.Email && m.Subject == "Kolejka");
        Assert.StartsWith("Treść kolejki", toA.Body);
        var token = toA.Body.Split("/wypisz/")[1].Trim();
        var anonymous = Factory.CreateClient();
        var state = await anonymous.GetFromJsonAsync<MailingSubscriptionDto>($"/api/mailing/subscription/{token}");
        Assert.Equal(("Jan", false), (state!.FirstName, state.OptedOut));
        var left = await (await anonymous.PostAsync($"/api/mailing/subscription/{token}/unsubscribe", null)).Content.ReadFromJsonAsync<MailingSubscriptionDto>();
        Assert.True(left!.OptedOut);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/mailing/subscription/nieznany")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync("/api/mailing/subscription/nieznany/unsubscribe", null)).StatusCode);

        var person = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/people/{a.Id}");
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, person.GetProperty("mailingOptOutAtUtc").ValueKind);
        var recipients = await admin.GetFromJsonAsync<List<MailingRecipientDto>>($"/api/mailing/campaigns/{campaign.Id}/recipients");
        Assert.True(recipients!.Single(r => r.PersonId == a.Id).OptedOut);

        var back = await (await anonymous.PostAsync($"/api/mailing/subscription/{token}/resubscribe", null)).Content.ReadFromJsonAsync<MailingSubscriptionDto>();
        Assert.False(back!.OptedOut);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync("/api/mailing/subscription/nieznany/resubscribe", null)).StatusCode);
    }

    [Fact]
    public async Task TheDailyWorkflow_KicksTheQueue_WithTheRemindersKey()
    {
        var anonymous = Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/reminders/mailing-queue/run", null)).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/mailing-queue/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");
        Assert.Equal(HttpStatusCode.Accepted, (await anonymous.SendAsync(request)).StatusCode);
    }
}
