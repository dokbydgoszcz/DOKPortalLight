import { Permissions } from '../core/auth/permissions';

export interface NavItem {
  label: string;
  icon: string;
  path: string;
  permission?: string;
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '◫', path: '/dashboard' },
  { label: 'Baza osób', icon: '◎', path: '/people' },
  { label: 'Kalendarz imienin', icon: '✿', path: '/name-days' },
  { label: 'Dokumenty i pisma', icon: '✎', path: '/documents', permission: Permissions.DocumentsView },
  { label: 'Mailing', icon: '✉', path: '/mailing', permission: Permissions.MailingView },
  { label: 'Kandydaci SKŚP', icon: '◉', path: '/candidates', permission: Permissions.CandidatesView },
  { label: 'Katechiści posłani', icon: '✦', path: '/missions', permission: Permissions.MissionsView },
  { label: 'Formatorzy', icon: '♙', path: '/formators', permission: Permissions.FormatorsView },
  { label: 'Parafie i giełda', icon: '⌂', path: '/parish-board', permission: Permissions.ParishNeedsView },
  { label: 'Rejestr parafii', icon: '✚', path: '/parishes', permission: Permissions.ParishesManage },
  { label: 'Budżet SKŚP', icon: '◈', path: '/budget/sksp', permission: Permissions.BudgetSkspView },
  { label: 'Podopieczni DOK', icon: '◍', path: '/dok-cases', permission: Permissions.DokCasesView },
  { label: 'Harmonogram i obecności', icon: '▦', path: '/meetings', permission: Permissions.MeetingsView },
  { label: 'Superwizje', icon: '◌', path: '/supervisions', permission: Permissions.SupervisionsView },
  { label: 'Absolwenci', icon: '✓', path: '/graduates', permission: Permissions.GraduatesView },
  { label: 'Budżet DOK', icon: '◈', path: '/budget/dok', permission: Permissions.BudgetDokView },
  { label: 'Użytkownicy i role', icon: '⚙', path: '/admin/users', permission: Permissions.UsersManage },
  { label: 'Uprawnienia ról', icon: '⚖', path: '/admin/permissions', permission: Permissions.PermissionsManage },
  { label: 'Audit log', icon: '⛨', path: '/audit-log', permission: Permissions.AuditLogView }
];
