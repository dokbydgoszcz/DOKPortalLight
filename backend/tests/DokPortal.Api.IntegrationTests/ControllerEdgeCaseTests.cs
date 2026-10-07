using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.DokCases;
using DokPortal.Application.Formators;
using DokPortal.Application.Meetings;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class FormatorsAndMeetingsEdgeCaseTests : IntegrationTestBase
{
    public FormatorsAndMeetingsEdgeCaseTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    [Fact]
    public async Task Formator_CanBeReadUpdatedAndDeleted_AndUnknownIdsReturnNotFound()
    {
        var admin = await AdminAsync();
        var person = await (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Pawel", LastName = "Nowicki" }))
            .Content.ReadFromJsonAsync<PersonDto>();
        var created = await (await admin.PostAsJsonAsync("/api/formators", new { PersonId = person!.Id, Function = "Moderator" }))
            .Content.ReadFromJsonAsync<FormatorDto>();

        Assert.Equal("Moderator", (await (await admin.GetAsync($"/api/formators/{created!.Id}")).Content.ReadFromJsonAsync<FormatorDto>())!.Function);

        var updateResponse = await admin.PutAsJsonAsync($"/api/formators/{created.Id}", new { PersonId = person.Id, Function = "Wykładowca" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Wykładowca", (await updateResponse.Content.ReadFromJsonAsync<FormatorDto>())!.Function);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/formators/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/formators/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/formators/{Guid.NewGuid()}", new { PersonId = person.Id, Function = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/formators/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Meeting_CanBeReadUpdatedAndDeleted_AndUnknownIdsReturnNotFound()
    {
        var admin = await AdminAsync();
        var created = await (await admin.PostAsJsonAsync("/api/meetings", new { GroupLabel = "Grupa A", MeetingDate = "2026-10-01" }))
            .Content.ReadFromJsonAsync<MeetingDto>();

        Assert.Equal("Grupa A", (await (await admin.GetAsync($"/api/meetings/{created!.Id}")).Content.ReadFromJsonAsync<MeetingDto>())!.GroupLabel);

        var updateResponse = await admin.PutAsJsonAsync($"/api/meetings/{created.Id}", new { GroupLabel = "Grupa B", MeetingDate = "2026-10-08", IsAttended = true });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<MeetingDto>();
        Assert.Equal("Grupa B", updated!.GroupLabel);
        Assert.True(updated.IsAttended);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/meetings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/meetings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/meetings/{Guid.NewGuid()}", new { MeetingDate = "2026-10-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/meetings/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task CaseDocument_ChangingStatusOfAnUnknownDocument_ReturnsNotFound()
    {
        var admin = await AdminAsync();

        var response = await admin.PutAsJsonAsync($"/api/dok-cases/{Guid.NewGuid()}/documents/{Guid.NewGuid()}", new { IsProvided = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class CaseDocumentFileTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public CaseDocumentFileTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Admin, Guid CaseId, Guid DocumentId)> SeedDocumentAsync()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var person = await (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" })).Content.ReadFromJsonAsync<PersonDto>();
        var catechist = await (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Anna", LastName = "Maj" })).Content.ReadFromJsonAsync<PersonDto>();
        var dokCase = await (await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = person!.Id, Path = "Confirmation", Stage = "Evangelization", CatechistPersonId = catechist!.Id
        })).Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);
        var document = await (await admin.PostAsJsonAsync($"/api/dok-cases/{dokCase!.Id}/documents", new { Name = "Metryka chrztu" }))
            .Content.ReadFromJsonAsync<CaseDocumentDto>();
        return (admin, dokCase.Id, document!.Id);
    }

    private static MultipartFormDataContent FileContent(string text, string fileName)
    {
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { bytes, "file", fileName } };
    }

    [Fact]
    public async Task UploadThenDownload_ReturnsTheSameFile_AndMarksTheDocumentAsProvided()
    {
        var (admin, caseId, documentId) = await SeedDocumentAsync();

        var upload = await admin.PostAsync($"/api/dok-cases/{caseId}/documents/{documentId}/upload", FileContent("PDF-DATA", "metryka.pdf"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var dto = await upload.Content.ReadFromJsonAsync<CaseDocumentDto>();
        Assert.True(dto!.IsProvided);
        Assert.Equal("metryka.pdf", dto.OriginalFileName);

        var download = await admin.GetAsync($"/api/dok-cases/{caseId}/documents/{documentId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("PDF-DATA", await download.Content.ReadAsStringAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("metryka.pdf", download.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Download_WithoutUpload_ReturnsNotFound()
    {
        var (admin, caseId, documentId) = await SeedDocumentAsync();

        var response = await admin.GetAsync($"/api/dok-cases/{caseId}/documents/{documentId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ToUnknownDocument_ReturnsNotFound()
    {
        var (admin, caseId, _) = await SeedDocumentAsync();

        var response = await admin.PostAsync($"/api/dok-cases/{caseId}/documents/{Guid.NewGuid()}/upload", FileContent("x", "a.pdf"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_OfAnEmptyFile_ReturnsBadRequest()
    {
        var (admin, caseId, documentId) = await SeedDocumentAsync();

        var response = await admin.PostAsync($"/api/dok-cases/{caseId}/documents/{documentId}/upload", FileContent("", "pusty.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_IsForbiddenForSuperwizor_AndAllowedForAdministrator()
    {
        var (admin, caseId, documentId) = await SeedDocumentAsync();
        var superwizor = await CreateAuthenticatedClientAsync($"sup-{Guid.NewGuid():N}@example.org", "Sekret123!", "Superwizor");

        var forbidden = await superwizor.PostAsync($"/api/dok-cases/{caseId}/documents/{documentId}/upload", FileContent("x", "a.pdf"));
        var allowed = await admin.PostAsync($"/api/dok-cases/{caseId}/documents/{documentId}/upload", FileContent("x", "a.pdf"));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}

public class UsersEdgeCaseTests : IntegrationTestBase
{
    public UsersEdgeCaseTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private static object NewUser(string email, string password = "Sekret123!", Guid? personId = null) =>
        new { Email = email, Password = password, PersonId = personId, Roles = Array.Empty<string>() };

    [Fact]
    public async Task Create_WithAnUnknownPerson_ReturnsBadRequest()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", NewUser($"nowy-{Guid.NewGuid():N}@example.org", personId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAWeakPasswordOrDuplicateEmail_ReturnsBadRequest()
    {
        var admin = await AdminAsync();
        var email = $"nowy-{Guid.NewGuid():N}@example.org";

        var weak = await admin.PostAsJsonAsync("/api/users", NewUser(email, password: "a"));
        var first = await admin.PostAsJsonAsync("/api/users", NewUser(email));
        var duplicate = await admin.PostAsJsonAsync("/api/users", NewUser(email));

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Create_ForAnExistingPerson_LinksTheAccountToThem()
    {
        var admin = await AdminAsync();
        var person = await (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Ewa", LastName = "Nowak" })).Content.ReadFromJsonAsync<PersonDto>();

        var response = await admin.PostAsJsonAsync("/api/users", NewUser($"ewa-{Guid.NewGuid():N}@example.org", personId: person!.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(person.Id, (await response.Content.ReadFromJsonAsync<UserDto>())!.PersonId);
    }

    [Fact]
    public async Task AssignRoles_ToAnUnknownUserOrWithAnUnknownRole_Fails()
    {
        var admin = await AdminAsync();
        var created = await (await admin.PostAsJsonAsync("/api/users", NewUser($"nowy-{Guid.NewGuid():N}@example.org"))).Content.ReadFromJsonAsync<UserDto>();

        var unknownUser = await admin.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/roles", new { Roles = new[] { "Biskup" } });
        var unknownRole = await admin.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { Roles = new[] { "NieistniejacaRola" } });

        Assert.Equal(HttpStatusCode.NotFound, unknownUser.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ChangesThePassword_AndRejectsUnknownUsersAndWeakPasswords()
    {
        var admin = await AdminAsync();
        var email = $"nowy-{Guid.NewGuid():N}@example.org";
        var created = await (await admin.PostAsJsonAsync("/api/users", NewUser(email))).Content.ReadFromJsonAsync<UserDto>();

        var unknownUser = await admin.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/reset-password", new { NewPassword = "Nowe12345!" });
        var weak = await admin.PutAsJsonAsync($"/api/users/{created!.Id}/reset-password", new { NewPassword = "a" });
        var ok = await admin.PutAsJsonAsync($"/api/users/{created.Id}/reset-password", new { NewPassword = "Nowe12345!" });

        Assert.Equal(HttpStatusCode.NotFound, unknownUser.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Nowe12345!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" })).StatusCode);
    }
}
