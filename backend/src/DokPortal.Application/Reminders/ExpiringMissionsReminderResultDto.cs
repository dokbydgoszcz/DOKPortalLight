namespace DokPortal.Application.Reminders;

public class ExpiringMissionsReminderResultDto
{
    /// <summary>Misje kończące się w ciągu 30 dni, o których jeszcze nie przypominano.</summary>
    public required int MissionsFound { get; init; }
    /// <summary>Katechiści, którzy dostali przypomnienie.</summary>
    public required int CatechistsNotified { get; init; }
    /// <summary>Osoby zarządzające misjami, które dostały zestawienie.</summary>
    public required int ManagersNotified { get; init; }
    public required int FailedSends { get; init; }
}
