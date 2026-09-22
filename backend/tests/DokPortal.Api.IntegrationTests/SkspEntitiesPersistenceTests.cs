using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SkspEntitiesPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SkspEntitiesPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedSkspEntities_CanBeReadBackInANewScope()
    {
        Guid candidateId, missionId, formatorId, needId, entryId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Test", LastName = "Kandydat",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var parish = new Parish { Id = Guid.NewGuid(), Name = "Testowa" };
            db.People.Add(person);
            db.Parishes.Add(parish);

            var candidate = new Candidate
            {
                Id = Guid.NewGuid(), PersonId = person.Id, Year = 1, OpinionsCollected = 0,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var mission = new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Parafia testowa",
                MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var formator = new Formator { Id = Guid.NewGuid(), PersonId = person.Id, Function = "Wykładowca" };
            var need = new ParishNeed
            {
                Id = Guid.NewGuid(), ParishId = parish.Id, Description = "Potrzeba testowa",
                CreatedAtUtc = DateTime.UtcNow
            };
            var entry = new BudgetEntry
            {
                Id = Guid.NewGuid(), Fund = BudgetFund.SKSP, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Description = "Wpis testowy", Category = "Test", Type = BudgetEntryType.Income, Amount = 100m,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Candidates.Add(candidate);
            db.CanonicalMissions.Add(mission);
            db.Formators.Add(formator);
            db.ParishNeeds.Add(need);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            candidateId = candidate.Id;
            missionId = mission.Id;
            formatorId = formator.Id;
            needId = need.Id;
            entryId = entry.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.Candidates.FindAsync(candidateId));
            Assert.NotNull(await db.CanonicalMissions.FindAsync(missionId));
            Assert.NotNull(await db.Formators.FindAsync(formatorId));
            Assert.NotNull(await db.ParishNeeds.FindAsync(needId));
            Assert.NotNull(await db.BudgetEntries.FindAsync(entryId));
        }
    }
}
