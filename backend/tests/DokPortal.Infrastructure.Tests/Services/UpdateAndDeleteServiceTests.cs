using DokPortal.Application.Candidates;
using DokPortal.Application.Formators;
using DokPortal.Application.Meetings;
using DokPortal.Application.Missions;
using DokPortal.Application.People;
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class UpdateAndDeleteServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Person> AddPersonAsync(AppDbContext db, string first = "Jan", string last = "Kowalski")
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    public class Missions
    {
        private static async Task<(MissionService Service, AppDbContext Db, Person Person, MissionDto Mission)> SeedAsync()
        {
            var db = CreateContext();
            var person = await AddPersonAsync(db);
            var service = new MissionService(db);
            var mission = await service.CreateAsync(new CreateMissionRequest
            {
                PersonId = person.Id, ServicePlace = "Parafia św. Jana",
                MissionStartDate = new DateOnly(2026, 1, 1), MissionEndDate = new DateOnly(2029, 1, 1)
            }, default);
            return (service, db, person, mission);
        }

        [Fact]
        public async Task SearchAsync_FiltersByPersonNameOrServicePlace_IgnoringCase()
        {
            var (service, db, person, _) = await SeedAsync();
            var other = await AddPersonAsync(db, "Anna", "Nowak");
            await service.CreateAsync(new CreateMissionRequest
            {
                PersonId = other.Id, ServicePlace = "Matki Bożej",
                MissionStartDate = new DateOnly(2026, 1, 1), MissionEndDate = new DateOnly(2027, 1, 1)
            }, default);

            var byLastName = await service.SearchAsync("KOWALSKI", 1, 20, default);
            var byPlace = await service.SearchAsync(" matki ", 1, 20, default);
            var all = await service.SearchAsync(null, 1, 20, default);

            Assert.Equal(person.Id, Assert.Single(byLastName.Items).PersonId);
            Assert.Equal("Matki Bożej", Assert.Single(byPlace.Items).ServicePlace);
            Assert.Equal(2, all.TotalCount);
        }

        [Fact]
        public async Task UpdateAsync_ChangesFields_AndReturnsNullForMissingMission()
        {
            var (service, _, person, mission) = await SeedAsync();

            var updated = await service.UpdateAsync(mission.Id, new UpdateMissionRequest
            {
                PersonId = person.Id, ServicePlace = "Parafia św. Piotra",
                MissionStartDate = new DateOnly(2026, 2, 1), MissionEndDate = new DateOnly(2030, 2, 1),
                GrantedDate = new DateOnly(2026, 1, 15), GrantedPlace = "Bydgoszcz", SupervisionGroup = "Grupa A"
            }, default);

            Assert.NotNull(updated);
            Assert.Equal("Parafia św. Piotra", updated!.ServicePlace);
            Assert.Equal(new DateOnly(2030, 2, 1), updated.MissionEndDate);
            Assert.Equal("Grupa A", updated.SupervisionGroup);
            Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateMissionRequest
            {
                PersonId = person.Id, ServicePlace = "x", MissionStartDate = new DateOnly(2026, 1, 1), MissionEndDate = new DateOnly(2027, 1, 1)
            }, default));
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesTheMission_AndRejectsMissingOrAlreadyDeleted()
        {
            var (service, db, _, mission) = await SeedAsync();

            Assert.True(await service.DeleteAsync(mission.Id, "user-1", default));

            Assert.Null(await service.GetByIdAsync(mission.Id, default));
            var stored = await db.CanonicalMissions.IgnoreQueryFilters().SingleAsync(m => m.Id == mission.Id);
            Assert.NotNull(stored.DeletedAtUtc);
            Assert.Equal("user-1", stored.DeletedBy);
            Assert.False(await service.DeleteAsync(mission.Id, "user-1", default));
            Assert.False(await service.DeleteAsync(Guid.NewGuid(), "user-1", default));
        }
    }

    public class Supervisions
    {
        private static CreateSupervisionRequest Request(Institution institution, string group, DateOnly date) =>
            new() { Institution = institution, GroupLabel = group, SupervisionDate = date, AttendeesCount = 8, ExpectedCount = 10, Topic = "Modlitwa", Conclusion = "Wnioski" };

        [Fact]
        public async Task GetAllAsync_FiltersByInstitution_NewestFirst()
        {
            await using var db = CreateContext();
            var service = new SupervisionService(db);
            await service.CreateAsync(Request(Institution.DOK, "Grupa A", new DateOnly(2026, 9, 1)), default);
            await service.CreateAsync(Request(Institution.SKSP, "Grupa B", new DateOnly(2026, 9, 2)), default);
            await service.CreateAsync(Request(Institution.DOK, "Grupa C", new DateOnly(2026, 9, 3)), default);

            var dok = await service.GetAllAsync(Institution.DOK, default);
            var all = await service.GetAllAsync(null, default);

            Assert.Equal(new[] { "Grupa C", "Grupa A" }, dok.Select(s => s.GroupLabel));
            Assert.Equal(3, all.Count);
        }

        [Fact]
        public async Task GetByIdAsync_UpdateAsync_DeleteAsync_WorkAndHandleMissingRecords()
        {
            await using var db = CreateContext();
            var service = new SupervisionService(db);
            var created = await service.CreateAsync(Request(Institution.DOK, "Grupa A", new DateOnly(2026, 9, 1)), default);

            Assert.Equal("Grupa A", (await service.GetByIdAsync(created.Id, default))!.GroupLabel);
            Assert.Null(await service.GetByIdAsync(Guid.NewGuid(), default));

            var updated = await service.UpdateAsync(created.Id, Request(Institution.SKSP, "Grupa Z", new DateOnly(2026, 10, 1)), default);
            Assert.Equal(Institution.SKSP, updated!.Institution);
            Assert.Equal("Grupa Z", updated.GroupLabel);
            Assert.Null(await service.UpdateAsync(Guid.NewGuid(), Request(Institution.DOK, "x", new DateOnly(2026, 1, 1)), default));

            Assert.True(await service.DeleteAsync(created.Id, "user-1", default));
            Assert.Empty(await service.GetAllAsync(null, default));
            var stored = await db.Supervisions.IgnoreQueryFilters().SingleAsync();
            Assert.Equal("user-1", stored.DeletedBy);
            Assert.False(await service.DeleteAsync(created.Id, "user-1", default));
        }
    }

    public class Meetings
    {
        [Fact]
        public async Task GetByIdAsync_UpdateAsync_DeleteAsync_WorkAndHandleMissingRecords()
        {
            await using var db = CreateContext();
            var service = new MeetingService(db);
            var created = await service.CreateAsync(new CreateMeetingRequest { GroupLabel = "Grupa A", MeetingDate = new DateOnly(2026, 10, 1) }, default);

            Assert.Equal("Grupa A", (await service.GetByIdAsync(created.Id, default))!.GroupLabel);
            Assert.Null(await service.GetByIdAsync(Guid.NewGuid(), default));

            var updated = await service.UpdateAsync(created.Id, new CreateMeetingRequest
            {
                GroupLabel = "Grupa B", MeetingDate = new DateOnly(2026, 10, 8), IsAttended = true, Notes = "Obecni wszyscy"
            }, default);
            Assert.Equal("Grupa B", updated!.GroupLabel);
            Assert.Equal(new DateOnly(2026, 10, 8), updated.MeetingDate);
            Assert.True(updated.IsAttended);
            Assert.Equal("Obecni wszyscy", updated.Notes);
            Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new CreateMeetingRequest { MeetingDate = new DateOnly(2026, 1, 1) }, default));

            Assert.True(await service.DeleteAsync(created.Id, "user-1", default));
            Assert.Empty(await service.GetAllAsync(default));
            Assert.Equal("user-1", (await db.Meetings.IgnoreQueryFilters().SingleAsync()).DeletedBy);
            Assert.False(await service.DeleteAsync(Guid.NewGuid(), "user-1", default));
        }

        [Fact]
        public async Task GetAllAsync_ShowsTheCasePersonName_AndNewestMeetingFirst()
        {
            await using var db = CreateContext();
            var person = await AddPersonAsync(db);
            var catechist = await AddPersonAsync(db, "Anna", "Maj");
            var dokCase = new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
                Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.DokCases.Add(dokCase);
            await db.SaveChangesAsync();
            var service = new MeetingService(db);
            await service.CreateAsync(new CreateMeetingRequest { GroupLabel = "Starsze", MeetingDate = new DateOnly(2026, 9, 1) }, default);
            await service.CreateAsync(new CreateMeetingRequest { DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 9, 5) }, default);

            var meetings = await service.GetAllAsync(default);

            Assert.Equal("Jan Kowalski", meetings[0].CaseLabel);
            Assert.Equal("Starsze", meetings[1].GroupLabel);
        }
    }

    public class Formators
    {
        [Fact]
        public async Task GetByIdAsync_UpdateAsync_DeleteAsync_WorkAndHandleMissingRecords()
        {
            await using var db = CreateContext();
            var first = await AddPersonAsync(db);
            var second = await AddPersonAsync(db, "Ewa", "Nowak");
            var service = new FormatorService(db);
            var created = await service.CreateAsync(new CreateFormatorRequest { PersonId = first.Id, Function = "Wykładowca" }, default);

            Assert.Equal("Jan Kowalski", (await service.GetByIdAsync(created.Id, default))!.PersonFullName);
            Assert.Null(await service.GetByIdAsync(Guid.NewGuid(), default));

            var updated = await service.UpdateAsync(created.Id, new CreateFormatorRequest { PersonId = second.Id, Function = "Moderator" }, default);
            Assert.Equal("Ewa Nowak", updated!.PersonFullName);
            Assert.Equal("Moderator", updated.Function);
            Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new CreateFormatorRequest { PersonId = first.Id, Function = "x" }, default));

            Assert.True(await service.DeleteAsync(created.Id, "user-1", default));
            Assert.Empty(await service.GetAllAsync(default));
            Assert.Equal("user-1", (await db.Formators.IgnoreQueryFilters().SingleAsync()).DeletedBy);
            Assert.False(await service.DeleteAsync(created.Id, "user-1", default));
        }
    }

    public class People
    {
        [Fact]
        public async Task UpdateAsync_ChangesEveryField_AndReturnsNullForMissingPerson()
        {
            await using var db = CreateContext();
            var service = new PersonService(db);
            var created = await service.CreateAsync(new CreatePersonRequest { FirstName = "Jan", LastName = "Kowalski" }, default);

            var updated = await service.UpdateAsync(created.Id, new UpdatePersonRequest
            {
                FirstName = "Janusz", LastName = "Nowak", Email = "j@example.org", Phone = "600100200",
                BirthDate = new DateOnly(1990, 5, 17), Notes = "Uwaga", NameDayMonth = 6, NameDayDay = 24
            }, default);

            Assert.Equal("Janusz Nowak", updated!.FullName);
            Assert.Equal("j@example.org", updated.Email);
            Assert.Equal(new DateOnly(1990, 5, 17), updated.BirthDate);
            Assert.Equal((6, 24), (updated.NameDayMonth, updated.NameDayDay));
            Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdatePersonRequest { FirstName = "x", LastName = "y" }, default));
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesThePerson_AndRejectsMissingOrAlreadyDeleted()
        {
            await using var db = CreateContext();
            var service = new PersonService(db);
            var created = await service.CreateAsync(new CreatePersonRequest { FirstName = "Jan", LastName = "Kowalski" }, default);

            Assert.True(await service.DeleteAsync(created.Id, "user-1", default));

            Assert.Null(await service.GetByIdAsync(created.Id, default));
            var stored = await db.People.IgnoreQueryFilters().SingleAsync();
            Assert.Equal("user-1", stored.DeletedBy);
            Assert.NotNull(stored.DeletedAtUtc);
            Assert.False(await service.DeleteAsync(created.Id, "user-1", default));
            Assert.False(await service.DeleteAsync(Guid.NewGuid(), "user-1", default));
        }
    }

    public class Candidates
    {
        [Fact]
        public async Task UpdateAsync_ChangesFields_AndReturnsNullForMissingCandidate()
        {
            await using var db = CreateContext();
            var person = await AddPersonAsync(db);
            var service = new CandidateService(db);
            var created = await service.CreateAsync(new CreateCandidateRequest
            {
                PersonId = person.Id, Year = 1, OpinionsCollected = 0
            }, default);

            var updated = await service.UpdateAsync(created.Id, new UpdateCandidateRequest
            {
                PersonId = person.Id, Year = 2, AttendancePercentage = 88, OpinionsCollected = 2,
                Retreats = new[] { new CandidateRetreatDto { Year = 2, IsCompleted = true } }
            }, default);

            Assert.Equal(2, updated!.Year);
            Assert.Equal(88, updated.AttendancePercentage);
            Assert.Equal(2, updated.OpinionsCollected);
            Assert.True(updated.Retreats.Single().IsCompleted);
            Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateCandidateRequest
            {
                PersonId = person.Id, Year = 1, OpinionsCollected = 0
            }, default));
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletesTheCandidate_AndRejectsMissingOrAlreadyDeleted()
        {
            await using var db = CreateContext();
            var person = await AddPersonAsync(db);
            var service = new CandidateService(db);
            var created = await service.CreateAsync(new CreateCandidateRequest
            {
                PersonId = person.Id, Year = 1, OpinionsCollected = 0
            }, default);

            Assert.True(await service.DeleteAsync(created.Id, "user-1", default));

            Assert.Null(await service.GetByIdAsync(created.Id, default));
            Assert.Equal("user-1", (await db.Candidates.IgnoreQueryFilters().SingleAsync()).DeletedBy);
            Assert.False(await service.DeleteAsync(created.Id, "user-1", default));
            Assert.False(await service.DeleteAsync(Guid.NewGuid(), "user-1", default));
        }
    }
}
