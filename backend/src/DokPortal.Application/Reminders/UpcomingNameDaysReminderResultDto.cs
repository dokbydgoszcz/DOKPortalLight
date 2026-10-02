namespace DokPortal.Application.Reminders;

public record UpcomingNameDaysReminderResultDto
{
    public required int NameDaysFound { get; init; }
    public required int RecipientsNotified { get; init; }
    public required int FailedSends { get; init; }
}
