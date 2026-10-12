namespace DokPortal.Application.DokCases;

/// <summary>Kto zmienił sprawę DOK (do historii zmian).</summary>
public record DokCaseActor(string UserId, string Email);
