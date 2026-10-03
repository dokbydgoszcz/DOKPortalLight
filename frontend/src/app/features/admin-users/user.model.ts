export interface AppUserAccount {
  id: string;
  email: string;
  personId: string | null;
  personFullName: string | null;
  roles: string[];
}

export interface CreateUserValue {
  email: string;
  password: string;
  roles: string[];
  personId?: string;
}
