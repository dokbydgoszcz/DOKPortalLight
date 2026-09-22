export const ALL_ROLES = [
  'Administrator', 'Biskup', 'DyrektorSKSP', 'DyrektorDOK', 'Superwizor', 'KatechistaProwadzacy'
] as const;

export interface AppUserAccount {
  id: string;
  email: string;
  personId: string | null;
  roles: string[];
}

export interface CreateUserValue {
  email: string;
  password: string;
  roles: string[];
}
