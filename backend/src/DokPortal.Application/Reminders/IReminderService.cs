namespace DokPortal.Application.Reminders;

public interface IReminderService
{
    Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct);
    Task<UpcomingMeetingsReminderResultDto> RunUpcomingMeetingsReminderAsync(CancellationToken ct);
    Task<UpcomingNameDaysReminderResultDto> RunUpcomingNameDaysReminderAsync(CancellationToken ct);
    /// <summary>Raz na misję: katechista i osoby zarządzające misjami dostają informację, że misja kończy się w ciągu 30 dni.</summary>
    Task<ExpiringMissionsReminderResultDto> RunExpiringMissionsReminderAsync(CancellationToken ct);
}
