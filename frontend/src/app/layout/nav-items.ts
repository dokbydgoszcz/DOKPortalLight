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
  { label: 'Parafie i giełda', icon: '⌂', path: '/parish-board', roles: ['Administrator', 'DyrektorSKSP'] },
  { label: 'Budżet SKŚP', icon: '◈', path: '/budget/sksp', roles: ['Administrator', 'DyrektorSKSP'] },
  { label: 'Podopieczni DOK', icon: '◍', path: '/dok-cases', roles: ['Administrator', 'DyrektorDOK', 'Superwizor', 'KatechistaProwadzacy', 'Biskup'] },
  { label: 'Harmonogram i obecności', icon: '▦', path: '/meetings', roles: ['Administrator', 'DyrektorDOK', 'KatechistaProwadzacy'] },
  { label: 'Superwizje', icon: '◌', path: '/supervisions', roles: ['Administrator', 'DyrektorDOK', 'DyrektorSKSP', 'Superwizor'] },
  { label: 'Użytkownicy i role', icon: '⚙', path: '/admin/users', roles: ['Administrator'] }
];
