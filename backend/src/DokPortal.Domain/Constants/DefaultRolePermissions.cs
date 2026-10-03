namespace DokPortal.Domain.Constants;

public static class DefaultRolePermissions
{
    public static readonly IReadOnlyDictionary<string, string[]> Grants = new Dictionary<string, string[]>
    {
        [AppRoles.Biskup] = new[]
        {
            Permissions.MissionsView, Permissions.DokCasesView, Permissions.DokCasesViewAll, Permissions.CaseDocumentsView
        },
        [AppRoles.DyrektorSKSP] = new[]
        {
            Permissions.PeopleManage, Permissions.PeopleExport,
            Permissions.CandidatesView, Permissions.CandidatesManage, Permissions.CandidatesExport,
            Permissions.MissionsView, Permissions.MissionsManage, Permissions.MissionsExport,
            Permissions.FormatorsView, Permissions.FormatorsManage, Permissions.FormatorsExport,
            Permissions.ParishNeedsView, Permissions.ParishNeedsManage,
            Permissions.BudgetSkspView, Permissions.BudgetSkspManage,
            Permissions.SupervisionsView, Permissions.SupervisionsManage, Permissions.SupervisionsExport,
            Permissions.DocumentsView, Permissions.DocumentsGenerate,
            Permissions.MailingView, Permissions.MailingManage
        },
        [AppRoles.DyrektorDOK] = new[]
        {
            Permissions.PeopleManage, Permissions.PeopleExport,
            Permissions.DokCasesView, Permissions.DokCasesViewAll, Permissions.DokCasesManage, Permissions.DokCasesExport,
            Permissions.CaseDocumentsView, Permissions.CaseDocumentsManage,
            Permissions.PastoralNotesView, Permissions.PastoralNotesWrite, Permissions.PastoralNotesReadAll,
            Permissions.MeetingsView, Permissions.MeetingsManage, Permissions.MeetingsExport,
            Permissions.SupervisionsView, Permissions.SupervisionsManage, Permissions.SupervisionsExport,
            Permissions.DocumentsView, Permissions.DocumentsGenerate,
            Permissions.MailingView, Permissions.MailingManage,
            Permissions.BudgetDokView, Permissions.BudgetDokManage,
            Permissions.GraduatesView
        },
        [AppRoles.Superwizor] = new[]
        {
            Permissions.DokCasesView, Permissions.DokCasesViewAll, Permissions.CaseDocumentsView,
            Permissions.SupervisionsView, Permissions.SupervisionsManage, Permissions.SupervisionsExport
        },
        [AppRoles.KatechistaProwadzacy] = new[]
        {
            Permissions.DokCasesView, Permissions.CaseDocumentsView, Permissions.CaseDocumentsManage,
            Permissions.PastoralNotesView, Permissions.PastoralNotesWrite,
            Permissions.MeetingsView, Permissions.MeetingsManage
        }
    };
}
