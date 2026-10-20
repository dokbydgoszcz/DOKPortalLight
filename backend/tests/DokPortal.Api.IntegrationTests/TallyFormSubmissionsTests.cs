using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DokPortal.Api.Integrations;
using DokPortal.Application.FormSubmissions;
using DokPortal.Application.People;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Webhook Tally → kolejka „Zgłoszenia z formularzy” → nowa osoba albo aktualizacja istniejącej.</summary>
public class TallyFormSubmissionsTests : IntegrationTestBase
{
    public TallyFormSubmissionsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string Payload(string submissionId, string first, string last, string email, string nameDay = "24 maja", string? phone = null) =>
        JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "FORM_RESPONSE",
            createdAt = "2026-10-20T13:01:42.000Z",
            data = new
            {
                responseId = "r" + submissionId,
                submissionId,
                respondentId = "x",
                formId = "LD12GG",
                formName = "Dane do systemu",
                createdAt = "2026-10-20T13:01:42.000Z",
                fields = new object[]
                {
                    new { key = "question_1", label = "Imię", type = "INPUT_TEXT", value = first + " " },
                    new { key = "question_2", label = "Nazwisko", type = "INPUT_TEXT", value = last },
                    new { key = "question_3", label = "Email", type = "INPUT_EMAIL", value = email },
                    new { key = "question_4", label = "telefon", type = "INPUT_PHONE_NUMBER", value = phone ?? "+48" + Random.Shared.Next(500_000_000, 899_999_999) },
                    new { key = "question_5", label = "Adres - Ulica nr domu / mieszkania", type = "INPUT_TEXT", value = "ul. Dabrowa 15 m. 69" },
                    new { key = "question_6", label = "Adres - kod pocztowy", type = "INPUT_TEXT", value = "85 147 Bydgoszcz" },
                    new { key = "question_7", label = "dzień imienin", type = "INPUT_TEXT", value = nameDay },
                    new
                    {
                        key = "question_8", label = "zdjęcie", type = "FILE_UPLOAD",
                        value = new[] { new { id = "f1", name = "ja.jpg", url = "https://storage.tally.so/private/ja.jpg?id=f1", mimeType = "image/jpeg", size = 1234 } }
                    },
                    new { key = "hidden", label = "utm", type = "HIDDEN_FIELDS", value = "x" }
                }
            }
        });

    private async Task<HttpResponseMessage> SendAsync(string json, string? secret = CustomWebApplicationFactory.TallySecret)
    {
        var body = Encoding.UTF8.GetBytes(json);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/tally/webhook") { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        if (secret is not null) request.Headers.Add(TallySignature.Header, TallySignature.Compute(secret, body));
        return await Factory.CreateClient().SendAsync(request);
    }

    private static string Unique() => new(Guid.NewGuid().ToString("N")[..8].Select(c => (char)(char.IsDigit(c) ? c - '0' + 'g' : c)).ToArray());

    private static async Task<FormSubmissionDto> Pending(HttpClient admin, string lastName)
    {
        var list = await admin.GetFromJsonAsync<List<FormSubmissionDto>>("/api/form-submissions", EnumJsonOptions);
        return Assert.Single(list!, s => s.Draft.LastName == lastName);
    }

    [Fact]
    public async Task TheWebhook_AcceptsOnlySignedRequests_AndIgnoresRepeats()
    {
        var json = Payload("s" + Unique(), "Ewa", "Podpisana" + Unique(), $"{Unique()}@example.org");

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(json, secret: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(json, secret: "zly-sekret")).StatusCode);

        var first = await SendAsync(json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("Created", await first.Content.ReadAsStringAsync());
        Assert.Contains("Duplicate", await (await SendAsync(json)).Content.ReadAsStringAsync());
        Assert.Contains("Ignored", await (await SendAsync("{\"eventType\":\"OTHER\",\"data\":{\"submissionId\":\"q\"}}")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync("nie json")).StatusCode);
    }

    [Fact]
    public async Task AReviewer_SeesTheCleanedUpDraft_AndCreatesANewPerson_WithTheFormPhoto()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "Katechista");
        var last = "Nowa" + Unique();
        var email = $"{Unique()}@example.org";
        await SendAsync(Payload("s" + Unique(), "marlena", last, email.ToUpperInvariant(), phone: "+48782265165"));

        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.GetAsync("/api/form-submissions")).StatusCode);
        Assert.True(await admin.GetFromJsonAsync<int>("/api/form-submissions/pending-count") >= 1);

        var submission = await Pending(admin, last);
        Assert.Equal("Marlena", submission.Draft.FirstName);
        Assert.Equal(email, submission.Draft.Email);
        Assert.Equal("782 265 165", submission.Draft.Phone);
        Assert.DoesNotContain(submission.Candidates, c => c.MatchedBy != "telefon");
        Assert.Equal("Dabrowa 15/69", submission.Draft.AddressStreet);
        Assert.Equal(("85-147", "Bydgoszcz"), (submission.Draft.AddressPostalCode, submission.Draft.AddressCity));
        Assert.Equal((24, 5), (submission.Draft.NameDayDay, submission.Draft.NameDayMonth));
        Assert.Equal(0, submission.Draft.PhotoIndex);
        Assert.DoesNotContain(submission.Fields, f => f.Label == "utm");

        var photo = await admin.GetAsync($"/api/form-submissions/{submission.Id}/files/0");
        Assert.Equal(FakeFormFileFetcher.Jpeg, await photo.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/form-submissions/{submission.Id}/files/5")).StatusCode);

        var applyResponse = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest
        {
            FirstName = submission.Draft.FirstName, LastName = last, Email = email, Phone = submission.Draft.Phone,
            AddressStreet = "Dąbrowa 15/69", AddressPostalCode = "85-147", AddressCity = "Bydgoszcz", NameDayDay = 24, NameDayMonth = 5
        });
        Assert.True(applyResponse.IsSuccessStatusCode, await applyResponse.Content.ReadAsStringAsync());
        var applied = await applyResponse.Content.ReadFromJsonAsync<FormSubmissionDto>(EnumJsonOptions);
        Assert.Equal(FormSubmissionStatus.Applied, applied!.Status);
        Assert.Equal($"Marlena {last}", applied.PersonFullName);

        var person = await admin.GetFromJsonAsync<PersonDto>($"/api/people/{applied.PersonId}", EnumJsonOptions);
        Assert.Equal(("Dąbrowa 15/69", email, 5), (person!.AddressStreet, person.Email, person.NameDayMonth));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest())).StatusCode);
        var done = await admin.GetFromJsonAsync<List<FormSubmissionDto>>("/api/form-submissions?status=Applied", EnumJsonOptions);
        Assert.Contains(done!, s => s.Id == submission.Id);

        // RODO: zanonimizowanie osoby usuwa też jej zgłoszenie z formularza.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/people/{applied.PersonId}/rodo/anonymize", null)).StatusCode);
        var afterGdpr = await admin.GetFromJsonAsync<List<FormSubmissionDto>>("/api/form-submissions?status=Applied", EnumJsonOptions);
        Assert.DoesNotContain(afterGdpr!, s => s.Id == submission.Id);
    }

    [Fact]
    public async Task WithoutASigningSecret_TheWebhookIsClosed()
    {
        using var factory = Factory.WithWebHostBuilder(b => b.UseSetting("Tally:SigningSecret", ""));
        var response = await factory.CreateClient().PostAsync("/api/integrations/tally/webhook", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task AnExistingPerson_IsSuggested_AndUpdatedWithoutLosingWhatTheFormLeftEmpty()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var last = "Stara" + Unique();
        var personId = await SeedPersonAsync(admin, "Joanna", last);
        var email = $"{Unique()}@example.org";
        var phone = "+48" + Random.Shared.Next(500_000_000, 899_999_999);
        await SendAsync(Payload("s" + Unique(), "Joanna", last, email, nameDay: "nie obchodzę", phone: phone));

        var submission = await Pending(admin, last);
        var candidate = Assert.Single(submission.Candidates);
        Assert.Equal((personId, "imię i nazwisko"), (candidate.PersonId, candidate.MatchedBy));
        Assert.Null(submission.Draft.NameDayMonth);

        var bad = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest { PersonId = personId, NameDayDay = 31, NameDayMonth = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var halfDate = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest { PersonId = personId, NameDayDay = 3 });
        Assert.Equal(HttpStatusCode.BadRequest, halfDate.StatusCode);
        var tooLong = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest { PersonId = personId, AddressCity = new string('x', 101) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var notEmail = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest { PersonId = personId, Email = "bez-malpy" });
        Assert.Equal(HttpStatusCode.BadRequest, notEmail.StatusCode);
        var missing = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest { PersonId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var response = await admin.PostAsJsonAsync($"/api/form-submissions/{submission.Id}/apply", new ApplyFormSubmissionRequest { PersonId = personId, Email = email, Phone = submission.Draft.Phone });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var person = await admin.GetFromJsonAsync<PersonDto>($"/api/people/{personId}", EnumJsonOptions);
        Assert.Equal(("Joanna", last, email, submission.Draft.Phone), (person!.FirstName, person.LastName, person.Email, person.Phone));

        // Drugi formularz tej samej osoby: dopasowanie po e-mailu i telefonie, a cudzy e-mail jest blokowany.
        var otherLast = "Inna" + Unique();
        await SendAsync(Payload("s" + Unique(), "Anna", otherLast, email, phone: phone));
        var second = await Pending(admin, otherLast);
        Assert.Equal("e-mail, telefon", Assert.Single(second.Candidates).MatchedBy);
        var taken = await admin.PostAsJsonAsync($"/api/form-submissions/{second.Id}/apply", new ApplyFormSubmissionRequest { FirstName = "Anna", LastName = otherLast, Email = email });
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Contains(last, await taken.Content.ReadAsStringAsync());
        var noName = await admin.PostAsJsonAsync($"/api/form-submissions/{second.Id}/apply", new ApplyFormSubmissionRequest { Email = $"{Unique()}@example.org" });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
    }

    [Fact]
    public async Task ASubmission_CanBeRejected_AndBroughtBack()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var last = "Odrzucona" + Unique();
        await SendAsync(Payload("s" + Unique(), "Test", last, $"{Unique()}@example.org"));
        var submission = await Pending(admin, last);

        var rejected = await (await admin.PostAsync($"/api/form-submissions/{submission.Id}/reject", null)).Content.ReadFromJsonAsync<FormSubmissionDto>(EnumJsonOptions);
        Assert.Equal(FormSubmissionStatus.Rejected, rejected!.Status);
        Assert.NotEmpty(rejected.ReviewedByEmail!);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/form-submissions/{submission.Id}/reject", null)).StatusCode);
        Assert.Contains(await admin.GetFromJsonAsync<List<FormSubmissionDto>>("/api/form-submissions?status=Rejected", EnumJsonOptions) ?? new(), s => s.Id == submission.Id);

        var reopened = await (await admin.PostAsync($"/api/form-submissions/{submission.Id}/reopen", null)).Content.ReadFromJsonAsync<FormSubmissionDto>(EnumJsonOptions);
        Assert.Equal(FormSubmissionStatus.Pending, reopened!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/form-submissions/{submission.Id}/reopen", null)).StatusCode);

        var unknown = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/form-submissions/{unknown}/reject", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/form-submissions/{unknown}/reopen", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/form-submissions/{unknown}/apply", new ApplyFormSubmissionRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/form-submissions/{unknown}/files/0")).StatusCode);
    }
}
