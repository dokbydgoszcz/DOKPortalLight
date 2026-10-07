namespace DokPortal.Application.Candidates;

/// <summary>Kto wykonał zmianę roku formacji (do śladu w historii).</summary>
public record CandidateActor(string UserId, string Email);
