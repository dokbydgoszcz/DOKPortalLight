import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { describe, it, expect } from 'vitest';
import { permissionGuard } from './permission.guard';
import { AuthService } from './auth.service';

function runGuard(permission: string, granted: string[]) {
  const dashboardTree = {} as UrlTree;
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { hasPermission: (p: string) => granted.includes(p) } },
      { provide: Router, useValue: { parseUrl: (url: string) => (url === '/dashboard' ? dashboardTree : null) } }
    ]
  });
  const result = TestBed.runInInjectionContext(() => permissionGuard(permission)({} as never, {} as never));
  return { result, dashboardTree };
}

describe('permissionGuard', () => {
  it('allows navigation when the user has the permission', () => {
    expect(runGuard('Candidates.View', ['Candidates.View']).result).toBe(true);
  });

  it('redirects to the dashboard when the permission is missing', () => {
    const { result, dashboardTree } = runGuard('Candidates.View', ['People.Manage']);

    expect(result).toBe(dashboardTree);
  });
});
