export interface NavItem {
  label: string;
  icon: string;
  path: string;
  roles: string[];
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '◫', path: '/dashboard', roles: [] },
  { label: 'Baza osób', icon: '◎', path: '/people', roles: [] },
  { label: 'Kandydaci SKŚP', icon: '◉', path: '/candidates', roles: ['Administrator', 'DyrektorSKSP'] },
  { label: 'Katechiści posłani', icon: '✦', path: '/missions', roles: ['Administrator', 'DyrektorSKSP', 'Biskup'] },
  { label: 'Formatorzy', icon: '♙', path: '/formators', roles: ['Administrator', 'DyrektorSKSP'] },
  { label: 'Użytkownicy i role', icon: '⚙', path: '/admin/users', roles: ['Administrator'] }
];
