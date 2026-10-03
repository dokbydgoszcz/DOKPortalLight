namespace DokPortal.Domain.Constants;

public static class Permissions
{
    public const string PeopleManage = "People.Manage";
    public const string PeopleExport = "People.Export";
    public const string ParishesManage = "Parishes.Manage";
    public const string ParishesExport = "Parishes.Export";
    public const string CandidatesView = "Candidates.View";
    public const string CandidatesManage = "Candidates.Manage";
    public const string CandidatesExport = "Candidates.Export";
    public const string MissionsView = "Missions.View";
    public const string MissionsManage = "Missions.Manage";
    public const string MissionsExport = "Missions.Export";
    public const string FormatorsView = "Formators.View";
    public const string FormatorsManage = "Formators.Manage";
    public const string FormatorsExport = "Formators.Export";
    public const string ParishNeedsView = "ParishNeeds.View";
    public const string ParishNeedsManage = "ParishNeeds.Manage";
    public const string BudgetSkspView = "BudgetSksp.View";
    public const string BudgetSkspManage = "BudgetSksp.Manage";
    public const string BudgetDokView = "BudgetDok.View";
    public const string BudgetDokManage = "BudgetDok.Manage";
    public const string DokCasesView = "DokCases.View";
    public const string DokCasesViewAll = "DokCases.ViewAll";
    public const string DokCasesManage = "DokCases.Manage";
    public const string DokCasesExport = "DokCases.Export";
    public const string CaseDocumentsView = "CaseDocuments.View";
    public const string CaseDocumentsManage = "CaseDocuments.Manage";
    public const string PastoralNotesView = "PastoralNotes.View";
    public const string PastoralNotesWrite = "PastoralNotes.Write";
    public const string PastoralNotesReadAll = "PastoralNotes.ReadAll";
    public const string MeetingsView = "Meetings.View";
    public const string MeetingsManage = "Meetings.Manage";
    public const string MeetingsExport = "Meetings.Export";
    public const string SupervisionsView = "Supervisions.View";
    public const string SupervisionsManage = "Supervisions.Manage";
    public const string SupervisionsExport = "Supervisions.Export";
    public const string DocumentsView = "Documents.View";
    public const string DocumentsGenerate = "Documents.Generate";
    public const string MailingView = "Mailing.View";
    public const string MailingManage = "Mailing.Manage";
    public const string GraduatesView = "Graduates.View";
    public const string UsersManage = "Users.Manage";
    public const string AuditLogView = "AuditLog.View";
    public const string PermissionsManage = "Permissions.Manage";
}

public sealed record PermissionInfo(string Name, string Module, string Label);

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionInfo> All = new PermissionInfo[]
    {
        new(Permissions.PeopleManage, "Osoby", "Dodawanie, edycja i usuwanie osób"),
        new(Permissions.PeopleExport, "Osoby", "Eksport osób do Excela"),
        new(Permissions.ParishesManage, "Rejestr parafii", "Dodawanie i usuwanie parafii"),
        new(Permissions.ParishesExport, "Rejestr parafii", "Eksport parafii do Excela"),
        new(Permissions.CandidatesView, "Kandydaci SKŚP", "Podgląd kandydatów"),
        new(Permissions.CandidatesManage, "Kandydaci SKŚP", "Dodawanie, edycja i usuwanie kandydatów"),
        new(Permissions.CandidatesExport, "Kandydaci SKŚP", "Eksport kandydatów do Excela"),
        new(Permissions.MissionsView, "Katechiści", "Podgląd misji kanonicznych"),
        new(Permissions.MissionsManage, "Katechiści", "Dodawanie, edycja i usuwanie misji"),
        new(Permissions.MissionsExport, "Katechiści", "Eksport misji do Excela"),
        new(Permissions.FormatorsView, "Formatorzy", "Podgląd formatorów"),
        new(Permissions.FormatorsManage, "Formatorzy", "Dodawanie, edycja i usuwanie formatorów"),
        new(Permissions.FormatorsExport, "Formatorzy", "Eksport formatorów do Excela"),
        new(Permissions.ParishNeedsView, "Parafie i giełda", "Podgląd potrzeb parafialnych"),
        new(Permissions.ParishNeedsManage, "Parafie i giełda", "Dodawanie, przypisywanie i usuwanie potrzeb"),
        new(Permissions.BudgetSkspView, "Budżet SKŚP", "Podgląd budżetu SKŚP"),
        new(Permissions.BudgetSkspManage, "Budżet SKŚP", "Dodawanie i usuwanie wpisów budżetu SKŚP"),
        new(Permissions.BudgetDokView, "Budżet DOK", "Podgląd budżetu DOK"),
        new(Permissions.BudgetDokManage, "Budżet DOK", "Dodawanie i usuwanie wpisów budżetu DOK"),
        new(Permissions.DokCasesView, "Podopieczni DOK", "Podgląd spraw DOK"),
        new(Permissions.DokCasesViewAll, "Podopieczni DOK", "Podgląd wszystkich spraw DOK i powiązanych dokumentów, notatek i spotkań (bez tego tylko własni podopieczni)"),
        new(Permissions.DokCasesManage, "Podopieczni DOK", "Dodawanie, edycja i usuwanie spraw DOK"),
        new(Permissions.DokCasesExport, "Podopieczni DOK", "Eksport spraw DOK do Excela"),
        new(Permissions.CaseDocumentsView, "Dokumenty spraw DOK", "Podgląd i pobieranie dokumentów sprawy"),
        new(Permissions.CaseDocumentsManage, "Dokumenty spraw DOK", "Dodawanie, edycja i wgrywanie dokumentów sprawy"),
        new(Permissions.PastoralNotesView, "Notatki duszpasterskie", "Podgląd własnych notatek"),
        new(Permissions.PastoralNotesWrite, "Notatki duszpasterskie", "Dodawanie notatek"),
        new(Permissions.PastoralNotesReadAll, "Notatki duszpasterskie", "Podgląd notatek wszystkich autorów"),
        new(Permissions.MeetingsView, "Spotkania", "Podgląd harmonogramu i obecności"),
        new(Permissions.MeetingsManage, "Spotkania", "Dodawanie, edycja i usuwanie spotkań"),
        new(Permissions.MeetingsExport, "Spotkania", "Eksport spotkań do Excela"),
        new(Permissions.SupervisionsView, "Superwizje", "Podgląd superwizji"),
        new(Permissions.SupervisionsManage, "Superwizje", "Dodawanie, edycja i usuwanie superwizji"),
        new(Permissions.SupervisionsExport, "Superwizje", "Eksport superwizji do Excela"),
        new(Permissions.DocumentsView, "Dokumenty i pisma", "Podgląd wygenerowanych dokumentów"),
        new(Permissions.DocumentsGenerate, "Dokumenty i pisma", "Generowanie dokumentów"),
        new(Permissions.MailingView, "Mailing", "Podgląd kampanii mailingowych"),
        new(Permissions.MailingManage, "Mailing", "Tworzenie i wysyłka kampanii"),
        new(Permissions.GraduatesView, "Absolwenci", "Dostęp do ekranu absolwentów"),
        new(Permissions.UsersManage, "Użytkownicy", "Zarządzanie użytkownikami i ich rolami"),
        new(Permissions.AuditLogView, "Dziennik audytu", "Podgląd dziennika audytu"),
        new(Permissions.PermissionsManage, "Uprawnienia", "Edycja uprawnień ról")
    };

    public static readonly IReadOnlySet<string> AllNames = All.Select(p => p.Name).ToHashSet();

    public static bool IsKnown(string name) => AllNames.Contains(name);
}
