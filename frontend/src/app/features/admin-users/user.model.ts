export interface AppUserAccount {
  id: string;
  email: string;
  personId: string | null;
  personFullName: string | null;
  roles: string[];
  /** Brak = konto aktywne (starsze odpowiedzi). */
  isActive?: boolean;
  /** Weryfikacja dwuetapowa (aplikacja uwierzytelniająca) włączona. */
  mfaEnabled?: boolean;
}

export interface CreateUserValue {
  email: string;
  password: string;
  roles: string[];
  personId?: string;
}
