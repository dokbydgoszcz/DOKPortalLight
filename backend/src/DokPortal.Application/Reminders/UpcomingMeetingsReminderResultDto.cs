namespace DokPortal.Application.Reminders;

public record UpcomingMeetingsReminderResultDto
{
    public required int MeetingsProcessed { get; init; }
    public required int EmailsSentToCatechists { get; init; }
    public required int FailedSends { get; init; }
}
