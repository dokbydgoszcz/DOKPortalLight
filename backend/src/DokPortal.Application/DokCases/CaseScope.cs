namespace DokPortal.Application.DokCases;

/// <summary>
/// Zakres spraw DOK dostępnych dla bieżącego użytkownika: wszystkie (uprawnienie DokCases.ViewAll)
/// albo tylko te, w których jest katechistą prowadzącym (PersonId konta).
/// </summary>
public sealed record CaseScope(bool ViewAll, Guid? PersonId)
{
    public static readonly CaseScope All = new(true, null);
}
