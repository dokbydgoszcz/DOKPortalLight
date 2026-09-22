export interface NavItem {
  label: string;
  icon: string;
  path: string;
  roles: string[];
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '◫', path: '/dashboard', roles: [] },
  { label: 'Baza osób', icon: '◎', path: '/people', roles: [] },
  { label: 'Użytkownicy i role', icon: '⚙', path: '/admin/users', roles: ['Administrator'] }
];
