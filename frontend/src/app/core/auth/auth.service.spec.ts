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

  it('remembers that the password must be changed, and forgets it after the change', async () => {
    const fakeToken = createFakeJwt({ sub: 'u', email: 'a@b.pl', exp: 9999999999 });
    const login = service.login('a@b.pl', 'Tymczasowe1!');
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`)
      .flush({ token: fakeToken, expiresAtUtc: '', roles: [], personId: null, mustChangePassword: true });
    await login;
    expect(service.mustChangePassword()).toBe(true);
    expect(service.email()).toBe('a@b.pl');

    const change = service.changePassword('Tymczasowe1!', 'MojeWlasne1!');
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/change-password`);
    expect(req.request.body).toEqual({ currentPassword: 'Tymczasowe1!', newPassword: 'MojeWlasne1!' });
    req.flush({ token: createFakeJwt({ sub: 'u', exp: 9999999999 }), expiresAtUtc: '', roles: [], personId: null, mustChangePassword: false });
    await change;

    expect(service.mustChangePassword()).toBe(false);
    expect(localStorage.getItem('dokportal.mustChangePassword')).toBeNull();
  });

  it('asks for a reset link and sets a new password with the code from the e-mail', async () => {
    const forgot = service.forgotPassword('a@b.pl');
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/forgot-password`).flush({ message: 'Sprawdź skrzynkę.' });
    expect(await forgot).toBe('Sprawdź skrzynkę.');

    const reset = service.resetPassword('u1', 'kod', 'Nowe12345!');
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/reset-password`);
    expect(req.request.body).toEqual({ userId: 'u1', token: 'kod', newPassword: 'Nowe12345!' });
    req.flush(null, { status: 204, statusText: 'No Content' });
    await reset;
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

  it('exposes the account id from the token, and none when logged out or the token is broken', () => {
    localStorage.setItem('dokportal.permissionsAware', '1');
    localStorage.setItem('dokportal.token', createFakeJwt({ sub: 'user-7', exp: 9999999999 }));
    expect(new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router)).userId()).toBe('user-7');

    localStorage.setItem('dokportal.token', createFakeJwt({ exp: 9999999999 }));
    expect(new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router)).userId()).toBeNull();

    localStorage.setItem('dokportal.token', 'not-a-jwt');
    expect(new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router)).userId()).toBeNull();

    localStorage.clear();
    expect(new AuthService(TestBed.inject(HttpClient), TestBed.inject(Router)).userId()).toBeNull();
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
  it('returns the MFA ticket instead of a session, then logs in with the code', async () => {
    const login = service.login('a@b.pl', 'Sekret123!');
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush({ mfaRequired: true, mfaToken: 'bilet' });
    expect(await login).toEqual({ mfaToken: 'bilet' });
    expect(service.isAuthenticated()).toBe(false);

    const verify = service.verifyMfa('bilet', '123456');
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/mfa/verify`);
    expect(req.request.body).toEqual({ mfaToken: 'bilet', code: '123456' });
    req.flush({ token: createFakeJwt({ sub: 'u', exp: 9999999999 }), expiresAtUtc: '', roles: [], personId: null });
    await verify;
    expect(service.isAuthenticated()).toBe(true);
  });

  it('manages MFA: status, setup, enable (new session + codes) and disable', async () => {
    const status = service.mfaStatus();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/mfa`).flush({ enabled: false, recoveryCodesLeft: 0 });
    expect((await status).enabled).toBe(false);

    const setup = service.setupMfa();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/mfa/setup`).flush({ sharedKey: 'KEY', otpAuthUri: 'otpauth://x' });
    expect((await setup).sharedKey).toBe('KEY');

    const enable = service.enableMfa('123456');
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/mfa/enable`)
      .flush({ recoveryCodes: ['AAAAA-BBBBB'], session: { token: createFakeJwt({ sub: 'u', exp: 9999999999 }), expiresAtUtc: '', roles: [], personId: null } });
    expect(await enable).toEqual(['AAAAA-BBBBB']);
    expect(service.isAuthenticated()).toBe(true);

    const disable = service.disableMfa('654321');
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/mfa/disable`);
    expect(req.request.body).toEqual({ code: '654321' });
    req.flush({ token: createFakeJwt({ sub: 'u', exp: 9999999999 }), expiresAtUtc: '', roles: [], personId: null });
    await disable;
  });
});
