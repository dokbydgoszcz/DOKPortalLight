namespace DokPortal.Domain.Entities;

/// <summary>
/// Autor udostępnił swoje notatki z danej sprawy osobie (katechiście prowadzącemu w chwili udostępnienia).
/// Obejmuje wszystkie notatki autora w sprawie, także dopisane później; autor może je cofnąć.
/// </summary>
public class PastoralNoteShare
{
    public Guid Id { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public required string AuthorUserId { get; set; }
    public Guid SharedWithPersonId { get; set; }
    public Person? SharedWithPerson { get; set; }
    public DateTime SharedAtUtc { get; set; }
}
