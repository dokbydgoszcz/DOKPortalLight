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

  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);
  readonly roles = computed(() => this.decodeClaimList(this.tokenSignal(), 'role'));
  readonly permissions = computed(() => this.decodeClaimList(this.tokenSignal(), 'permission'));

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null {
    return this.tokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/login`, { email, password })
    );
    localStorage.setItem(STORAGE_KEY, response.token);
    localStorage.setItem(PERMISSIONS_AWARE_KEY, '1');
    this.tokenSignal.set(response.token);
  }

  logout(reason?: 'idle'): void {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(PERMISSIONS_AWARE_KEY);
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
