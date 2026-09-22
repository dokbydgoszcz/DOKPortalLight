using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SharedToolsEntitiesPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SharedToolsEntitiesPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedSharedToolsEntities_CanBeReadBackInANewScope()
    {
        Guid personId, documentId, campaignId, logId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Ewa", LastName = "Nowak", NameDayMonth = 12, NameDayDay = 24,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.Add(person);

            var document = new GeneratedDocument
            {
                Id = Guid.NewGuid(), Template = DocumentTemplate.LetterToBishop, PersonId = person.Id,
                GeneratedByUserId = "user-1", AdditionalNotes = "Test", CreatedAtUtc = DateTime.UtcNow
            };
            var campaign = new MailingCampaign
            {
                Id = Guid.NewGuid(), Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp,
                RecipientCount = 5, Status = CampaignStatus.Draft, CreatedAtUtc = DateTime.UtcNow
            };
            var logEntry = new AuditLogEntry
            {
                Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow, UserId = "user-1", UserEmail = "user@example.org",
                Action = "ReadPastoralNotes", ObjectDescription = "Ewa Nowak", Result = AuditResult.Blocked
            };

            db.GeneratedDocuments.Add(document);
            db.MailingCampaigns.Add(campaign);
            db.AuditLogEntries.Add(logEntry);
            await db.SaveChangesAsync();

            personId = person.Id;
            documentId = document.Id;
            campaignId = campaign.Id;
            logId = logEntry.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var person = await db.People.FindAsync(personId);
            Assert.NotNull(person);
            Assert.Equal(12, person!.NameDayMonth);
            Assert.Equal(24, person.NameDayDay);
            Assert.NotNull(await db.GeneratedDocuments.FindAsync(documentId));
            Assert.NotNull(await db.MailingCampaigns.FindAsync(campaignId));
            Assert.NotNull(await db.AuditLogEntries.FindAsync(logId));
        }
    }
}
