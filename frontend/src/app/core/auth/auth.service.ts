import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  roles: string[];
  personId: string | null;
  mustChangePassword?: boolean;
}

interface DecodedToken {
  sub?: string;
  email?: string;
  role?: string | string[];
  permission?: string | string[];
  personId?: string;
  exp?: number;
}

const STORAGE_KEY = 'dokportal.token';
const PERMISSIONS_AWARE_KEY = 'dokportal.permissionsAware';
const MUST_CHANGE_KEY = 'dokportal.mustChangePassword';

function readMustChange(): boolean {
  try {
    return localStorage.getItem(MUST_CHANGE_KEY) === '1';
  } catch {
    return false;
  }
}

function readStoredToken(): string | null {
  const token = localStorage.getItem(STORAGE_KEY);
  if (token && localStorage.getItem(PERMISSIONS_AWARE_KEY) !== '1') {
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
  return token;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenSignal = signal<string | null>(readStoredToken());
  /** Hasło nadał administrator – przed pracą trzeba ustawić własne. */
  readonly mustChangePassword = signal(readMustChange());

  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);
  readonly roles = computed(() => this.decodeClaimList(this.tokenSignal(), 'role'));
  readonly permissions = computed(() => this.decodeClaimList(this.tokenSignal(), 'permission'));
  /** Identyfikator zalogowanego konta (claim „sub”), np. do rozpoznania własnych notatek. */
  readonly userId = computed(() => {
    const token = this.tokenSignal();
    return token ? this.decodeToken(token)?.sub ?? null : null;
  });
  /** Login (e-mail) zalogowanego konta. */
  readonly email = computed(() => {
    const token = this.tokenSignal();
    return token ? this.decodeToken(token)?.email ?? null : null;
  });

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null {
    return this.tokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/login`, { email, password })
    );
    this.storeSession(response);
  }

  /** Zmiana własnego hasła; serwer zwraca nową sesję (stare tokeny wygasają). */
  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/change-password`, { currentPassword, newPassword })
    );
    this.storeSession(response);
  }

  async forgotPassword(email: string): Promise<string> {
    const response = await firstValueFrom(
      this.http.post<{ message: string }>(`${environment.apiBaseUrl}/api/auth/forgot-password`, { email })
    );
    return response.message;
  }

  async resetPassword(userId: string, token: string, newPassword: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${environment.apiBaseUrl}/api/auth/reset-password`, { userId, token, newPassword })
    );
  }

  private storeSession(response: LoginResponse): void {
    localStorage.setItem(STORAGE_KEY, response.token);
    localStorage.setItem(PERMISSIONS_AWARE_KEY, '1');
    if (response.mustChangePassword) {
      localStorage.setItem(MUST_CHANGE_KEY, '1');
    } else {
      localStorage.removeItem(MUST_CHANGE_KEY);
    }
    this.mustChangePassword.set(!!response.mustChangePassword);
    this.tokenSignal.set(response.token);
  }

  logout(reason?: 'idle' | 'expired'): void {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(PERMISSIONS_AWARE_KEY);
    localStorage.removeItem(MUST_CHANGE_KEY);
    this.mustChangePassword.set(false);
    this.tokenSignal.set(null);
    this.router.navigateByUrl('/login', reason ? { state: { reason } } : undefined);
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    return roles.some(role => this.hasRole(role));
  }

  hasPermission(permission: string): boolean {
    return this.permissions().includes(permission);
  }

  hasAnyPermission(permissions: string[]): boolean {
    return permissions.some(permission => this.hasPermission(permission));
  }

  private decodeClaimList(token: string | null, claim: 'role' | 'permission'): string[] {
    if (!token) return [];
    const value = this.decodeToken(token)?.[claim];
    if (!value) return [];
    return Array.isArray(value) ? value : [value];
  }

  private decodeToken(token: string): DecodedToken | null {
    try {
      const payload = token.split('.')[1];
      const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      return JSON.parse(json) as DecodedToken;
    } catch {
      return null;
    }
  }
}
