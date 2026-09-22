import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

function createFakeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigateByUrl: vi.fn() } }
      ]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('stores the token and exposes decoded roles after a successful login', async () => {
    const fakeToken = createFakeJwt({ role: 'Administrator', sub: 'user-1', email: 'a@b.pl', exp: 9999999999 });

    const loginPromise = service.login('a@b.pl', 'secret');
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    req.flush({ token: fakeToken, expiresAtUtc: new Date().toISOString(), roles: ['Administrator'], personId: null });
    await loginPromise;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.hasRole('Administrator')).toBe(true);
  });

  it('clears the token on logout', () => {
    localStorage.setItem('dokportal.token', createFakeJwt({ role: 'Administrator', exp: 9999999999 }));
    service = TestBed.inject(AuthService);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('dokportal.token')).toBeNull();
  });
});
