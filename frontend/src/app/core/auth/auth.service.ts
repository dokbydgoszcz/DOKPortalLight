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
  personId?: string;
  exp?: number;
}

const STORAGE_KEY = 'dokportal.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenSignal = signal<string | null>(localStorage.getItem(STORAGE_KEY));

  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);
  readonly roles = computed(() => this.decodeRoles(this.tokenSignal()));

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null {
    return this.tokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/login`, { email, password })
    );
    localStorage.setItem(STORAGE_KEY, response.token);
    this.tokenSignal.set(response.token);
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.tokenSignal.set(null);
    this.router.navigateByUrl('/login');
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    return roles.some(role => this.hasRole(role));
  }

  private decodeRoles(token: string | null): string[] {
    if (!token) return [];
    const decoded = this.decodeToken(token);
    if (!decoded?.role) return [];
    return Array.isArray(decoded.role) ? decoded.role : [decoded.role];
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
