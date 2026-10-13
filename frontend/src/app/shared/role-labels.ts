/** Nazwy ról po polsku (role własne pokazujemy tak, jak je nazwano). */
export const ROLE_LABELS: Record<string, string> = {
  Administrator: 'Administrator',
  Biskup: 'Biskup',
  DyrektorSKSP: 'Dyrektor SKŚP',
  DyrektorDOK: 'Dyrektor DOK',
  Superwizor: 'Superwizor',
  KatechistaProwadzacy: 'Katechista prowadzący'
};

export const roleLabel = (role: string): string => ROLE_LABELS[role] ?? role;
