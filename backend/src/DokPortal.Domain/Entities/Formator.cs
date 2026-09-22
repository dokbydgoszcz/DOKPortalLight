namespace DokPortal.Domain.Entities;

public class Formator
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string Function { get; set; }
}
