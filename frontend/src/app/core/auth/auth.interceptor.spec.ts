import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, vi } from 'vitest';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { ToastService } from '../notifications/toast.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  const logout = vi.fn();

  function setup(token: string | null) {
    logout.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { token, logout, isAuthenticated: () => token !== null } }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  it('adds an Authorization header when a token is present', () => {
    setup('fake-token');
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe('Bearer fake-token');
  });

  it('does not add an Authorization header when there is no token', () => {
    setup(null);
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBe(false);
  });

  it('logs out with an explanation when the server rejects the session (401)', () => {
    setup('fake-token');
    let failed = false;
    http.get('/api/test').subscribe({ error: () => (failed = true) });

    httpMock.expectOne('/api/test').flush('x', { status: 401, statusText: 'Unauthorized' });

    expect(logout).toHaveBeenCalledWith('expired');
    expect(failed).toBe(true);
    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toEqual(['Sesja wygasła – zaloguj się ponownie.']);
  });

  it('does not log out on other errors, nor on a 401 without a session (e.g. a wrong password)', () => {
    setup('fake-token');
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush('x', { status: 403, statusText: 'Forbidden' });
    expect(logout).not.toHaveBeenCalled();

    TestBed.resetTestingModule();
    setup(null);
    http.post('/api/auth/login', {}).subscribe({ error: () => {} });
    httpMock.expectOne('/api/auth/login').flush('x', { status: 401, statusText: 'Unauthorized' });
    expect(logout).not.toHaveBeenCalled();
  });
});
