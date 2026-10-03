import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient } from '@angular/common/http';
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

  it('decodes permission claims given as a single string or as an array', () => {
    localStorage.setItem('dokportal.permissionsAware', '1');
    const single = createFakeJwt({ role: 'Biskup', permission: 'Missions.View', exp: 9999999999 });
    localStorage.setItem('dokportal.token', single);
    const first = new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router));
    expect(first.permissions()).toEqual(['Missions.View']);

    const many = createFakeJwt({ role: 'DyrektorDOK', permission: ['DokCases.View', 'DokCases.Manage'], exp: 9999999999 });
    localStorage.setItem('dokportal.token', many);
    const second = new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router));
    expect(second.hasPermission('DokCases.Manage')).toBe(true);
    expect(second.hasPermission('People.Manage')).toBe(false);
    expect(second.hasAnyPermission(['People.Manage', 'DokCases.View'])).toBe(true);
    expect(second.hasAnyPermission([])).toBe(false);
  });

  it('treats a stored token without the permissions flag as logged out', () => {
    localStorage.setItem('dokportal.token', createFakeJwt({ role: 'Administrator', exp: 9999999999 }));
    localStorage.removeItem('dokportal.permissionsAware');

    const fresh = new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router));

    expect(fresh.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('dokportal.token')).toBeNull();
  });

  it('keeps a stored token when the permissions flag is present', () => {
    localStorage.setItem('dokportal.token', createFakeJwt({ role: 'Administrator', exp: 9999999999 }));
    localStorage.setItem('dokportal.permissionsAware', '1');

    const fresh = new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router));

    expect(fresh.isAuthenticated()).toBe(true);
  });

  it('treats a malformed token as having no roles or permissions', () => {
    localStorage.setItem('dokportal.permissionsAware', '1');
    for (const broken of ['not-a-jwt', 'a.%%%.c', 'a.bm90LWpzb24.c']) {
      localStorage.setItem('dokportal.token', broken);
      const fresh = new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router));

      expect(fresh.roles()).toEqual([]);
      expect(fresh.permissions()).toEqual([]);
      expect(fresh.hasAnyRole(['Administrator'])).toBe(false);
    }
  });

  it('checks roles one at a time and in groups', () => {
    localStorage.setItem('dokportal.permissionsAware', '1');
    localStorage.setItem('dokportal.token', createFakeJwt({ role: ['Biskup', 'Superwizor'], exp: 9999999999 }));
    const fresh = new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router));

    expect(fresh.hasRole('Biskup')).toBe(true);
    expect(fresh.hasRole('Administrator')).toBe(false);
    expect(fresh.hasAnyRole(['Administrator', 'Superwizor'])).toBe(true);
  });

  it('sets the flag on login and clears it on logout', async () => {
    const token = createFakeJwt({ role: 'Administrator', permission: 'People.Manage', exp: 9999999999 });

    const loginPromise = service.login('a@b.pl', 'secret');
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`)
      .flush({ token, expiresAtUtc: new Date().toISOString(), roles: ['Administrator'], personId: null });
    await loginPromise;
    expect(localStorage.getItem('dokportal.permissionsAware')).toBe('1');
    expect(service.hasPermission('People.Manage')).toBe(true);

    service.logout();
    expect(localStorage.getItem('dokportal.permissionsAware')).toBeNull();
    expect(service.permissions()).toEqual([]);
  });
});
