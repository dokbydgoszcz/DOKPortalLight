namespace DokPortal.Application.Reminders;

public interface IReminderService
{
    Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct);
    Task<UpcomingMeetingsReminderResultDto> RunUpcomingMeetingsReminderAsync(CancellationToken ct);
    Task<UpcomingNameDaysReminderResultDto> RunUpcomingNameDaysReminderAsync(CancellationToken ct);
}
