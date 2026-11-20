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
  /** false = konto zaproszone, użytkownik jeszcze nie ustawił hasła. Brak = ma hasło (starsze odpowiedzi). */
  hasPassword?: boolean;
}

export interface CreateUserValue {
  email: string;
  /** Hasło tymczasowe; bez niego konto dostaje zaproszenie e-mailem. */
  password?: string;
  roles: string[];
  personId?: string;
}
