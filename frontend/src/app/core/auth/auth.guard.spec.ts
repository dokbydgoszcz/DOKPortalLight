import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

function runGuard(authenticated: boolean, mustChange = false, url = '/dashboard') {
  TestBed.resetTestingModule();
  const trees: Record<string, UrlTree> = { '/login': {} as UrlTree, '/konto/haslo': {} as UrlTree };
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { isAuthenticated: () => authenticated, mustChangePassword: () => mustChange } },
      { provide: Router, useValue: { parseUrl: (u: string) => trees[u] ?? null } }
    ]
  });
  const result = TestBed.runInInjectionContext(() => authGuard({} as never, { url } as never));
  return { result, trees };
}

describe('authGuard', () => {
  it('lets an authenticated user through', () => {
    expect(runGuard(true).result).toBe(true);
  });

  it('redirects an anonymous user to the login page', () => {
    const { result, trees } = runGuard(false);

    expect(result).toBe(trees['/login']);
  });

  it('sends a user with an admin-given password to the password change first', () => {
    const { result, trees } = runGuard(true, true, '/people');

    expect(result).toBe(trees['/konto/haslo']);
    expect(runGuard(true, true, '/konto/haslo').result).toBe(true);
  });
});
