namespace DokPortal.Application.Reminders;

public interface IReminderService
{
    Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct);
}
