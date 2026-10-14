import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AppUserAccount, CreateUserValue } from './user.model';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/users`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<AppUserAccount[]>(this.baseUrl);
  }

  listRoles() {
    return this.http.get<string[]>(`${this.baseUrl}/roles`);
  }

  create(value: CreateUserValue) {
    return this.http.post<AppUserAccount>(this.baseUrl, value);
  }

  assignRoles(id: string, roles: string[]) {
    return this.http.put<AppUserAccount>(`${this.baseUrl}/${id}/roles`, { roles });
  }

  setPerson(id: string, personId: string | null) {
    return this.http.put<AppUserAccount>(`${this.baseUrl}/${id}/person`, { personId });
  }

  setActive(id: string, isActive: boolean) {
    return this.http.put<AppUserAccount>(`${this.baseUrl}/${id}/active`, { isActive });
  }

  /** Poprawia login (e-mail) konta. */
  changeLogin(id: string, email: string) {
    return this.http.put<AppUserAccount>(`${this.baseUrl}/${id}/login`, { email });
  }

  /** Wyłącza MFA na koncie (np. zgubiony telefon). */
  resetMfa(id: string) {
    return this.http.delete<AppUserAccount>(`${this.baseUrl}/${id}/mfa`);
  }

  resetPassword(id: string, newPassword: string) {
    return this.http.put<void>(`${this.baseUrl}/${id}/reset-password`, { newPassword });
  }
}
