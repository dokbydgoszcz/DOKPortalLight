using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DokEntitiesPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DokEntitiesPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedDokEntities_CanBeReadBackInANewScope()
    {
        Guid caseId, documentId, noteId, meetingId, supervisionId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var catechist = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.AddRange(person, catechist);

            var dokCase = new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
                CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.DokCases.Add(dokCase);

            var document = new CaseDocument { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, Name = "Metryka chrztu", CreatedAtUtc = DateTime.UtcNow };
            var note = new PastoralNote { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, AuthorUserId = "user-1", Content = "Notatka testowa", CreatedAtUtc = DateTime.UtcNow };
            var meeting = new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAtUtc = DateTime.UtcNow };
            var supervision = new Supervision
            {
                Id = Guid.NewGuid(), Institution = Institution.DOK, GroupLabel = "Grupa A",
                SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAtUtc = DateTime.UtcNow
            };

            db.CaseDocuments.Add(document);
            db.PastoralNotes.Add(note);
            db.Meetings.Add(meeting);
            db.Supervisions.Add(supervision);
            await db.SaveChangesAsync();

            caseId = dokCase.Id;
            documentId = document.Id;
            noteId = note.Id;
            meetingId = meeting.Id;
            supervisionId = supervision.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.DokCases.FindAsync(caseId));
            Assert.NotNull(await db.CaseDocuments.FindAsync(documentId));
            Assert.NotNull(await db.PastoralNotes.FindAsync(noteId));
            Assert.NotNull(await db.Meetings.FindAsync(meetingId));
            Assert.NotNull(await db.Supervisions.FindAsync(supervisionId));
        }
    }
}
