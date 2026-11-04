using System.Net;
using DokPortal.Application.FormSubmissions;
using DokPortal.Infrastructure.Services;
using Xunit;

namespace DokPortal.Infrastructure.Tests.FormSubmissions;

/// <summary>Prawdziwe odpowiedzi z formularza „Dane do systemu” i to, co z nich wychodzi.</summary>
public class FormSubmissionParserTests
{
    [Theory]
    [InlineData("85-149 Bydgoszcz ", "85-149", "Bydgoszcz")]
    [InlineData("86 005", "86-005", null)]
    [InlineData("89300", "89-300", null)]
    [InlineData("85 ' 790 Bydgoszcz ", "85-790", "Bydgoszcz")]
    [InlineData("86-022 Strzelce Górne ", "86-022", "Strzelce Górne")]
    [InlineData("brak", "brak", null)]
    [InlineData("  ", null, null)]
    public void PostalCodes_AreNormalized_AndTheCityIsTakenFromTheRest(string input, string? code, string? city) =>
        Assert.Equal((code, city), FormSubmissionParser.SplitPostal(input));

    [Theory]
    [InlineData("ul. Dabrowa 15/69", null, "Dabrowa 15/69")]
    [InlineData("Stroma 13A m. 31", null, "Stroma 13A/31")]
    [InlineData("Skłodowskiej Curie 56 m 403", null, "Skłodowskiej Curie 56/403")]
    [InlineData("UL. KS. KORDECKIEGO 14, 5", null, "Ks. Kordeckiego 14/5")]
    [InlineData("Chłodna, 16/6", null, "Chłodna 16/6")]
    [InlineData("M.Kromera 6/ 30", null, "M.Kromera 6/30")]
    [InlineData("ul. Ks. Ignacego Skorupki  6/17", null, "Ks. Ignacego Skorupki 6/17")]
    [InlineData("Wyrzysk, ul. Wiejska 9", "Wyrzysk", "Wiejska 9")]
    [InlineData(" ", null, null)]
    public void Streets_AreCleanedUp(string input, string? city, string? street) =>
        Assert.Equal((city, street), FormSubmissionParser.SplitStreet(input));

    [Theory]
    [InlineData("24 maja", 24, 5)]
    [InlineData("4 Maj", 4, 5)]
    [InlineData("27 lipiec ", 27, 7)]
    [InlineData("21 sierpień", 21, 8)]
    [InlineData("13 marca", 13, 3)]
    [InlineData("29 października", 29, 10)]
    [InlineData("1 pazdziernika", 1, 10)]
    [InlineData("23.10.", 23, 10)]
    [InlineData("16.09", 16, 9)]
    [InlineData("02.02", 2, 2)]
    [InlineData("7/05", 7, 5)]
    [InlineData("Nie obchodzę ", null, null)]
    [InlineData("31.02", null, null)]
    [InlineData("12 smoka", null, null)]
    [InlineData("", null, null)]
    public void NameDays_AreReadFromWordsAndNumbers(string input, int? day, int? month) =>
        Assert.Equal((day, month), FormSubmissionParser.ParseNameDay(input));

    [Theory]
    [InlineData("Bzdawka- Piątkowska", "Bzdawka-Piątkowska")]
    [InlineData("  Marlena  ", "Marlena")]
    [InlineData("KOWALSKA", "Kowalska")]
    [InlineData("anna maria", "Anna Maria")]
    [InlineData("McDonald", "McDonald")]
    [InlineData(" ", null)]
    public void Names_AreTrimmedAndCapitalized(string input, string? expected) =>
        Assert.Equal(expected, FormSubmissionParser.NormalizeName(input));

    [Theory]
    [InlineData("+48782265165", "782 265 165")]
    [InlineData("0048 501 291 002", "501 291 002")]
    [InlineData("+44 20 7946 0958", "+44 20 7946 0958")]
    [InlineData(null, null)]
    public void Phones_AreFormatted(string? input, string? expected) =>
        Assert.Equal(expected, FormSubmissionParser.NormalizePhone(input));

    [Fact]
    public void TheDraft_MapsFieldsByTheirLabels()
    {
        var draft = FormSubmissionParser.Draft(new[]
        {
            new FormSubmissionField("Imię i nazwisko", "INPUT_TEXT", "Jan  Maria Kowalski"),
            new FormSubmissionField("E-mail", "INPUT_EMAIL", " Jan@Example.org "),
            new FormSubmissionField("Numer", "INPUT_PHONE_NUMBER", "+48600100200"),
            new FormSubmissionField("Miejscowość", "INPUT_TEXT", "BYDGOSZCZ"),
            new FormSubmissionField("Adres", "INPUT_TEXT", "Długa 1"),
            new FormSubmissionField("Kod pocztowy", "INPUT_TEXT", "85-001 Toruń"),
            new FormSubmissionField("Pusty", "INPUT_TEXT", null),
            new FormSubmissionField("Skan", "FILE_UPLOAD", null, new[] { new FormSubmissionFile("skan.pdf", "https://storage.tally.so/a", "application/pdf", 1) }),
            new FormSubmissionField("Foto", "FILE_UPLOAD", null, new[] { new FormSubmissionFile("ja.PNG", "https://storage.tally.so/b", null, null) })
        });
        Assert.Equal(("Jan", "Maria Kowalski", "jan@example.org", "600 100 200"), (draft.FirstName, draft.LastName, draft.Email, draft.Phone));
        Assert.Equal(("Długa 1", "85-001", "Bydgoszcz", 1), (draft.AddressStreet, draft.AddressPostalCode, draft.AddressCity, draft.PhotoIndex));
    }

    [Theory]
    [InlineData("85-796", "Bydgoszcz")]
    [InlineData("86-005", null)]
    [InlineData(null, null)]
    public void BydgoszczIsKnownFromItsPostalCodes(string? code, string? city) => Assert.Equal(city, FormSubmissionParser.CityFromPostalCode(code));

    [Fact]
    public void TheDraft_TakesTheCityFromTheStreet_WhenNothingElseGivesIt()
    {
        var draft = FormSubmissionParser.Draft(new[]
        {
            new FormSubmissionField("Imię", "INPUT_TEXT", "Benedykta"),
            new FormSubmissionField("Adres - Ulica nr domu / mieszkania", "INPUT_TEXT", "Wyrzysk, ul. Wiejska 9"),
            new FormSubmissionField("Adres - kod pocztowy", "INPUT_TEXT", "89-300")
        });
        Assert.Equal(("Wiejska 9", "89-300", "Wyrzysk", null), (draft.AddressStreet, draft.AddressPostalCode, draft.AddressCity, draft.PhotoIndex));
    }

    [Fact]
    public void TallyPayloads_AreRead_IncludingChoicesFlagsAndNumbers()
    {
        const string json = """
        {"eventType":"FORM_RESPONSE","createdAt":"2026-10-20T10:00:00Z","data":{"responseId":"r1","formId":"F","formName":" Formularz ",
         "fields":[
           {"key":"a","label":"Wybór","type":"MULTIPLE_CHOICE","value":["o1"],"options":[{"id":"o1","text":"Tak"}]},
           {"key":"b","label":"Zgoda","type":"CHECKBOX","value":true},
           {"key":"c","label":"Nie","type":"CHECKBOX","value":false},
           {"key":"d","label":"Ile","type":"INPUT_NUMBER","value":3},
           {"key":"e","type":"INPUT_TEXT","value":null},
           {"key":"f","label":"Obiekt","type":"X","value":{"a":1}},
           {"key":"g","label":"Tablica","type":"X","value":[1]},
           {"key":"h","label":"Wyliczone","type":"CALCULATED_FIELDS","value":1}
         ]}}
        """;
        var tally = FormSubmissionParser.ParseTally(json)!;
        Assert.Equal(("r1", "Formularz", new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc)), (tally.SubmissionId, tally.FormName, tally.SubmittedAtUtc));
        Assert.Equal(new[] { "Tak", "tak", "nie", "3", null, "{\"a\":1}", "1" }, tally.Fields.Select(f => f.Text));
        Assert.Equal("e", tally.Fields[4].Label);
        Assert.Null(FormSubmissionParser.ParseTally("[]"));
        Assert.Null(FormSubmissionParser.ParseTally("{\"eventType\":\"FORM_RESPONSE\"}"));
        Assert.Null(FormSubmissionParser.ParseTally("{\"data\":{}}"));
    }

    [Theory]
    [InlineData("https://storage.tally.so/private/a.jpg", true)]
    [InlineData("https://tally.so/a", true)]
    [InlineData("http://storage.tally.so/a", false)]
    [InlineData("https://tally.so.evil.example/a", false)]
    [InlineData("nie-adres", false)]
    public void Files_AreFetchedOnlyFromTally(string url, bool allowed) => Assert.Equal(allowed, TallyFileFetcher.IsAllowed(url));

    private sealed class StubHandler(HttpStatusCode status, byte[] body, string? type) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(body);
            if (type is not null) content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }

    [Fact]
    public async Task TheFetcher_DownloadsTallyFiles_AndRefusesOthersAndExpiredLinks()
    {
        var ok = new TallyFileFetcher(new HttpClient(new StubHandler(HttpStatusCode.OK, new byte[] { 1, 2 }, "image/png")));
        var file = await ok.FetchAsync(new FormSubmissionFile("a.png", "https://storage.tally.so/a", null, null), default);
        Assert.Equal(new byte[] { 1, 2 }, file.Content);
        Assert.Equal(("image/png", "a.png"), (file.ContentType, file.Name));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ok.FetchAsync(new FormSubmissionFile("a", "https://example.org/a", null, null), default));
        var gone = new TallyFileFetcher(new HttpClient(new StubHandler(HttpStatusCode.Forbidden, Array.Empty<byte>(), null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gone.FetchAsync(new FormSubmissionFile("a", "https://storage.tally.so/a", "image/jpeg", null), default));
        var huge = new TallyFileFetcher(new HttpClient(new StubHandler(HttpStatusCode.OK, new byte[TallyFileFetcher.MaxBytes + 1], null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => huge.FetchAsync(new FormSubmissionFile("a", "https://storage.tally.so/a", null, null), default));
    }
}

/// <summary>Odczyt zgłoszeń z API Tally (to, co nie doszło webhookiem).</summary>
public class TallyApiTests
{
    private const string Page = """
    {"page":1,"limit":100,"hasMore":false,
     "questions":[
       {"id":"q1","type":"INPUT_TEXT","title":"Imię "},
       {"id":"q2","type":"INPUT_TEXT","title":"Nazwisko"},
       {"id":"q3","type":"MULTIPLE_CHOICE","title":"Rok"},
       {"id":"q4","type":"FILE_UPLOAD","title":"zdjęcie"},
       {"id":"q5","type":"INPUT_TEXT","title":"Usunięte","isDeleted":true},
       {"id":"q6","type":"HIDDEN_FIELDS","title":"utm"},
       {"id":"q7","type":"INPUT_TEXT","title":"dzień imienin"},
       {"title":"bez id"}
     ],
     "submissions":[
       {"id":"s1","isCompleted":true,"submittedAt":"2026-11-03T22:17:46.000Z","responses":[
         {"questionId":"q2","answer":"Szweda"},
         {"questionId":"q1","answer":"Roma"},
         {"questionId":"q3","answer":["opt1"],"formattedAnswer":"Rok II"},
         {"questionId":"q4","answer":[{"id":"f","name":"ja.jpg","url":"https://storage.tally.so/private/ja.jpg","mimeType":"image/jpeg","size":5}],"formattedAnswer":"ja.jpg"},
         {"questionId":"q5","answer":"x"},
         {"questionId":"q6","answer":"y"},
         {"questionId":"q7"},
         {"answer":"bez pytania"}
       ]},
       {"id":"s2","isCompleted":false,"submittedAt":"2026-11-03T22:00:00Z","responses":[]},
       {"isCompleted":true},
       {"id":"s3","submittedAt":"zła data"}
     ]}
    """;

    [Fact]
    public void Answers_AreMappedToTheQuestions_InFormOrder()
    {
        var events = FormSubmissionParser.ParseTallyApi(Page, "LD12GG", "Dane do systemu");
        Assert.Equal(new[] { "s1", "s3" }, events.Select(e => e.SubmissionId));
        var s1 = events[0];
        Assert.Equal(("LD12GG", "Dane do systemu", new DateTime(2026, 11, 3, 22, 17, 46, DateTimeKind.Utc)), (s1.FormId, s1.FormName, s1.SubmittedAtUtc));
        Assert.Equal(new[] { "Imię", "Nazwisko", "Rok", "zdjęcie", "dzień imienin" }, s1.Fields.Select(f => f.Label));
        Assert.Equal(new[] { "Roma", "Szweda", "Rok II", null, null }, s1.Fields.Select(f => f.Text));
        Assert.Equal("https://storage.tally.so/private/ja.jpg", Assert.Single(s1.Fields[3].Files!).Url);
        Assert.All(events[1].Fields, f => Assert.Null(f.Text));
        Assert.Empty(FormSubmissionParser.ParseTallyApi("{}", "F", null));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static TallyApiClient Client(Handler handler, string? key = "klucz")
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager();
        configuration["Tally:ApiKey"] = key;
        return new(new HttpClient(handler), configuration);
    }

    [Fact]
    public async Task TheClient_ReadsAllPagesWithTheKey()
    {
        var handler = new Handler(r => r.RequestUri!.AbsolutePath switch
        {
            "/forms/LD12GG" => Json("""{"id":"LD12GG","name":" Dane do systemu "}"""),
            _ when r.RequestUri.Query.Contains("page=1") => Json(Page.Replace("\"hasMore\":false", "\"hasMore\":true")),
            _ => Json("""{"hasMore":false,"questions":[],"submissions":[{"id":"s9","submittedAt":"2026-11-01T00:00:00Z"}]}""")
        });
        var events = await Client(handler).ListSubmissionsAsync("LD12GG", new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), default);
        Assert.Equal(new[] { "s1", "s3", "s9" }, events.Select(e => e.SubmissionId));
        Assert.Equal("Dane do systemu", events[0].FormName);
        Assert.All(handler.Requests, r => Assert.Equal("Bearer klucz", r.Headers.Authorization!.ToString()));
        Assert.Contains("startDate=2026-10-04T00%3A00%3A00.000Z", handler.Requests[1].RequestUri!.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "klucz API")]
    [InlineData(HttpStatusCode.NotFound, "Tally__FormIds")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    public async Task TheClient_ExplainsTallyErrors(HttpStatusCode status, string expected)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Client(new Handler(_ => Json("{}", status))).ListSubmissionsAsync("X", DateTime.UtcNow, default));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task WithoutAKey_NothingIsAsked()
    {
        var handler = new Handler(_ => Json("{}"));
        var client = Client(handler, key: null);
        Assert.False(client.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListSubmissionsAsync("X", DateTime.UtcNow, default));
        Assert.Empty(handler.Requests);
        // Formularz bez nazwy w odpowiedzi – zgłoszenia i tak przychodzą.
        var noName = new Handler(r => r.RequestUri!.AbsolutePath == "/forms/X" ? Json("{}") : Json("""{"submissions":[{"id":"a"}]}"""));
        Assert.Null((await Client(noName).ListSubmissionsAsync("X", DateTime.UtcNow, default)).Single().FormName);
    }
}
