namespace DokPortal.Application.Missions;

/// <summary>Osoba, która ukończyła formację w SKŚP i czeka na udzielenie posługi (nie ma jeszcze misji).</summary>
public class PendingCatechistDto
{
    public required Guid CandidateId { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? ParishName { get; init; }
    public required DateOnly FormationCompletedOn { get; init; }
}
