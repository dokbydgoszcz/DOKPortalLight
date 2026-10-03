using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;

namespace DokPortal.Infrastructure.Services;

public static class CaseScopeExtensions
{
    /// <summary>Ogranicza sprawy do zakresu użytkownika. Brak PersonId bez ViewAll = żadne sprawy.</summary>
    public static IQueryable<DokCase> ForScope(this IQueryable<DokCase> cases, CaseScope scope) =>
        scope.ViewAll ? cases
        : scope.PersonId is { } personId ? cases.Where(c => c.CatechistPersonId == personId)
        : cases.Where(_ => false);

    /// <summary>
    /// Ogranicza spotkania do widocznych dla użytkownika: przy pełnym zakresie wszystkie,
    /// inaczej spotkania powiązane ze sprawami w jego zakresie oraz zajęcia grupowe, których jest właścicielem.
    /// </summary>
    public static IQueryable<Meeting> ForScope(this IQueryable<Meeting> meetings, AppDbContext db, CaseScope scope)
    {
        if (scope.ViewAll) return meetings;

        var visibleCaseIds = db.DokCases.ForScope(scope).Select(c => c.Id);
        var personId = scope.PersonId;
        return meetings.Where(m =>
            (m.DokCaseId != null && visibleCaseIds.Contains(m.DokCaseId.Value)) ||
            (personId != null && m.CatechistPersonId == personId));
    }
}
