import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

function runGuard(authenticated: boolean) {
  const loginTree = {} as UrlTree;
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { isAuthenticated: () => authenticated } },
      { provide: Router, useValue: { parseUrl: (url: string) => (url === '/login' ? loginTree : null) } }
    ]
  });
  const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
  return { result, loginTree };
}

describe('authGuard', () => {
  it('lets an authenticated user through', () => {
    expect(runGuard(true).result).toBe(true);
  });

  it('redirects an anonymous user to the login page', () => {
    const { result, loginTree } = runGuard(false);

    expect(result).toBe(loginTree);
  });
});
