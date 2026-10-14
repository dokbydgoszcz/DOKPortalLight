namespace DokPortal.Application.SkspClasses;

public class SkspClassAttendeeDto
{
    public required Guid CandidateId { get; init; }
    public required string PersonFullName { get; init; }
    public bool? IsPresent { get; init; }
}

public class SkspClassDto
{
    public required Guid Id { get; init; }
    public required int Year { get; init; }
    public required DateOnly ClassDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public required string Topic { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<SkspClassAttendeeDto> Attendees { get; init; }
}

public class SaveSkspClassRequest
{
    public required int Year { get; init; }
    public required DateOnly ClassDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public required string Topic { get; init; }
    public string? Notes { get; init; }
}

public class SkspAttendanceItem
{
    public required Guid CandidateId { get; init; }
    public bool? IsPresent { get; init; }
}

public interface ISkspClassService
{
    Task<IReadOnlyList<SkspClassDto>> GetAllAsync(int? year, CancellationToken ct);
    /// <summary>Zakłada zajęcia i wpisuje na listę obecności wszystkich kandydatów w formacji na tym roku.</summary>
    Task<SkspClassDto> CreateAsync(SaveSkspClassRequest request, CancellationToken ct);
    /// <summary>Zmienia dane zajęć (bez zmiany roku – lista obecności zostaje).</summary>
    Task<SkspClassDto?> UpdateAsync(Guid id, SaveSkspClassRequest request, CancellationToken ct);
    /// <summary>Zapisuje obecność wskazanych kandydatów (tylko tych z listy zajęć).</summary>
    Task<SkspClassDto?> SetAttendanceAsync(Guid id, IReadOnlyList<SkspAttendanceItem> items, CancellationToken ct);
    /// <summary>Dopisuje do listy kandydatów, którzy doszli na ten rok po założeniu zajęć.</summary>
    Task<SkspClassDto?> RefreshAttendeesAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
}
