namespace DokPortal.Application.Reminders;

public record MissingDocumentsReminderResultDto
{
    public required int CasesProcessed { get; init; }
    public required int EmailsSentToCatechists { get; init; }
    public required bool DirectorsSummarySent { get; init; }
    public required int FailedSends { get; init; }
}
