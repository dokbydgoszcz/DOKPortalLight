# Uprawnienia oparte o polityki — część 2: frontend — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menu, trasy, przyciski eksportu i przyciski akcji (Dodaj/Edytuj/Usuń/Wyślij…) w Angularze sterowane uprawnieniami z claimów `permission` w JWT zamiast własnych list ról.

**Architecture:** `AuthService` dekoduje claimy `permission` (`hasPermission`, `hasAnyPermission`) i odrzuca tokeny sprzed wdrożenia uprawnień (flaga w `localStorage` ustawiana przy logowaniu). Stałe uprawnień w `core/auth/permissions.ts` (lustro katalogu z backendu). Dyrektywa strukturalna `*appHasPermission` ukrywa elementy w szablonach; `permissionGuard(permission)` chroni trasy; `nav-items.ts` i `export-lists.ts` używają pola `permission` zamiast `roles`.

**Tech Stack:** Angular 22 (standalone, signals), Vitest + `TestBed`.

**Spec:** `docs/superpowers/specs/2026-10-02-permissions-design.md` (część 2). Backend (część 1) jest już w repo i wystawia claimy `permission`.

## Global Constraints

- Nazwy uprawnień mają format `Moduł.Akcja` i są identyczne z backendowym katalogiem (`backend/src/DokPortal.Domain/Constants/Permissions.cs`) — 41 pozycji.
- Pozycje menu bez `permission` są widoczne dla każdego zalogowanego (Dashboard, Baza osób, Kalendarz imienin).
- Token bez flagi `dokportal.permissionsAware` w `localStorage` jest traktowany jak wylogowanie (stare tokeny nie mają claimów `permission`).
- Backend pozostaje autorytatywny — frontend tylko ukrywa elementy i przekierowuje.
- Mapowanie trasa/pozycja menu → uprawnienie: Dokumenty i pisma `Documents.View`; Mailing `Mailing.View`; Kandydaci SKŚP `Candidates.View`; Katechiści posłani `Missions.View`; Formatorzy `Formators.View`; Parafie i giełda `ParishNeeds.View`; Rejestr parafii `Parishes.Manage`; Budżet SKŚP `BudgetSksp.View`; Podopieczni DOK `DokCases.View`; Harmonogram i obecności `Meetings.View`; Superwizje `Supervisions.View`; Absolwenci `Graduates.View`; Budżet DOK `BudgetDok.View`; Użytkownicy i role `Users.Manage`; Audit log `AuditLog.View`.
- Komunikaty i etykiety UI po polsku.
- **Commity lokalne na `master`, BEZ `git push`** — produkcyjny backend jest chwilowo zatrzymany (limit Azure Free), push wymaga zgody użytkownika. Nie commitować `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/` (stage'ować tylko wskazane pliki).
- Stopka commita: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Komendy frontendu uruchamiać z `C:\eu02_install\DOKPortalLight\frontend` (`npx ng test --watch=false`).

---

### Task 1: Stałe uprawnień i `AuthService` (claimy, flaga starych tokenów)

**Files:**
- Create: `frontend/src/app/core/auth/permissions.ts`
- Modify: `frontend/src/app/core/auth/auth.service.ts`
- Test: `frontend/src/app/core/auth/permissions.spec.ts`, `frontend/src/app/core/auth/auth.service.spec.ts`

**Interfaces:**
- Produces: `Permissions` (obiekt `as const`, klucze PascalCase np. `Permissions.PeopleManage === 'People.Manage'`), `ALL_PERMISSIONS: readonly string[]`; `AuthService.permissions: Signal<string[]>`, `AuthService.hasPermission(permission: string): boolean`, `AuthService.hasAnyPermission(permissions: string[]): boolean`. `AuthService.hasRole`/`hasAnyRole`/`roles` zostają (nagłówek pokazuje pigułki ról).

- [ ] **Step 1: Testy (czerwone)**

`permissions.spec.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { ALL_PERMISSIONS, Permissions } from './permissions';

describe('Permissions', () => {
  it('has 41 unique names in Module.Action format', () => {
    expect(ALL_PERMISSIONS.length).toBe(41);
    expect(new Set(ALL_PERMISSIONS).size).toBe(41);
    for (const name of ALL_PERMISSIONS) {
      expect(name).toMatch(/^[A-Za-z]+\.[A-Za-z]+$/);
    }
  });

  it('exposes the constants used by the menu and guards', () => {
    expect(Permissions.PeopleManage).toBe('People.Manage');
    expect(Permissions.BudgetDokView).toBe('BudgetDok.View');
    expect(Permissions.PermissionsManage).toBe('Permissions.Manage');
  });
});
```

W `auth.service.spec.ts` zmień import na `import { HttpClient, provideHttpClient } from '@angular/common/http';` i dopisz w `describe` (po istniejących testach):

```ts
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
```

- [ ] **Step 2: Uruchom — czerwone (brak `permissions.ts`)**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|FAIL|Could not resolve|passed|failed" | head -6
```

- [ ] **Step 3: Implementacja**

`permissions.ts`:

```ts
export const Permissions = {
  PeopleManage: 'People.Manage',
  PeopleExport: 'People.Export',
  ParishesManage: 'Parishes.Manage',
  ParishesExport: 'Parishes.Export',
  CandidatesView: 'Candidates.View',
  CandidatesManage: 'Candidates.Manage',
  CandidatesExport: 'Candidates.Export',
  MissionsView: 'Missions.View',
  MissionsManage: 'Missions.Manage',
  MissionsExport: 'Missions.Export',
  FormatorsView: 'Formators.View',
  FormatorsManage: 'Formators.Manage',
  FormatorsExport: 'Formators.Export',
  ParishNeedsView: 'ParishNeeds.View',
  ParishNeedsManage: 'ParishNeeds.Manage',
  BudgetSkspView: 'BudgetSksp.View',
  BudgetSkspManage: 'BudgetSksp.Manage',
  BudgetDokView: 'BudgetDok.View',
  BudgetDokManage: 'BudgetDok.Manage',
  DokCasesView: 'DokCases.View',
  DokCasesManage: 'DokCases.Manage',
  DokCasesExport: 'DokCases.Export',
  CaseDocumentsView: 'CaseDocuments.View',
  CaseDocumentsManage: 'CaseDocuments.Manage',
  PastoralNotesView: 'PastoralNotes.View',
  PastoralNotesWrite: 'PastoralNotes.Write',
  PastoralNotesReadAll: 'PastoralNotes.ReadAll',
  MeetingsView: 'Meetings.View',
  MeetingsManage: 'Meetings.Manage',
  MeetingsExport: 'Meetings.Export',
  SupervisionsView: 'Supervisions.View',
  SupervisionsManage: 'Supervisions.Manage',
  SupervisionsExport: 'Supervisions.Export',
  DocumentsView: 'Documents.View',
  DocumentsGenerate: 'Documents.Generate',
  MailingView: 'Mailing.View',
  MailingManage: 'Mailing.Manage',
  GraduatesView: 'Graduates.View',
  UsersManage: 'Users.Manage',
  AuditLogView: 'AuditLog.View',
  PermissionsManage: 'Permissions.Manage'
} as const;

export const ALL_PERMISSIONS: readonly string[] = Object.values(Permissions);
```

`auth.service.ts` — zamień całą zawartość klasy/stałych na:

```ts
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
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -6
```
Expected: wszystkie przechodzą (istniejące testy nie ustawiają tokenu w `localStorage` przed utworzeniem serwisu poza nowymi).

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/core/auth/permissions.ts frontend/src/app/core/auth/auth.service.ts frontend/src/app/core/auth/permissions.spec.ts frontend/src/app/core/auth/auth.service.spec.ts
git commit -m "$(cat <<'EOF'
Frontend: AuthService czyta uprawnienia z JWT i odrzuca stare tokeny

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Dyrektywa `*appHasPermission`

**Files:**
- Create: `frontend/src/app/shared/permissions/has-permission.directive.ts`
- Test: `frontend/src/app/shared/permissions/has-permission.directive.spec.ts`

**Interfaces:**
- Consumes: `AuthService.hasPermission`, `ALL_PERMISSIONS` (Task 1).
- Produces: `HasPermissionDirective` (selektor `[appHasPermission]`, standalone) — renderuje szablon tylko, gdy `hasPermission(...)` zwraca `true`; rzuca `Error('Nieznane uprawnienie: X')` dla nazwy spoza katalogu (literówka w szablonie wychodzi w testach komponentów).

- [ ] **Step 1: Test (czerwony)** — `has-permission.directive.spec.ts`:

```ts
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, it, expect } from 'vitest';
import { HasPermissionDirective } from './has-permission.directive';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `<button *appHasPermission="'People.Manage'">Dodaj</button>`
})
class KnownPermissionHost {}

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `<button *appHasPermission="'People.Fly'">Dodaj</button>`
})
class UnknownPermissionHost {}

function render<T>(component: new () => T, granted: string[]) {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [{ provide: AuthService, useValue: { hasPermission: (p: string) => granted.includes(p) } }]
  });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture;
}

describe('HasPermissionDirective', () => {
  it('renders the element when the user has the permission', () => {
    const fixture = render(KnownPermissionHost, ['People.Manage']);

    expect(fixture.nativeElement.querySelector('button')).not.toBeNull();
  });

  it('does not render the element without the permission', () => {
    const fixture = render(KnownPermissionHost, ['Candidates.Manage']);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('throws for a permission name that is not in the catalog', () => {
    expect(() => render(UnknownPermissionHost, ['People.Manage'])).toThrow('Nieznane uprawnienie: People.Fly');
  });
});
```

- [ ] **Step 2: Uruchom — czerwone** (`Could not resolve "./has-permission.directive"`).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|Could not resolve|passed|failed" | head -4
```

- [ ] **Step 3: Implementacja** — `has-permission.directive.ts`:

```ts
import { Directive, Input, TemplateRef, ViewContainerRef, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ALL_PERMISSIONS } from '../../core/auth/permissions';

@Directive({ selector: '[appHasPermission]', standalone: true })
export class HasPermissionDirective {
  private readonly auth = inject(AuthService);
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);

  @Input({ required: true }) set appHasPermission(permission: string) {
    if (!ALL_PERMISSIONS.includes(permission)) {
      throw new Error(`Nieznane uprawnienie: ${permission}`);
    }
    this.viewContainer.clear();
    if (this.auth.hasPermission(permission)) {
      this.viewContainer.createEmbeddedView(this.templateRef);
    }
  }
}
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -6
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/shared/permissions
git commit -m "$(cat <<'EOF'
Frontend: dodaj dyrektywę *appHasPermission

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Guard tras, menu i powłoka na uprawnieniach

**Files:**
- Create: `frontend/src/app/core/auth/permission.guard.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`, `frontend/src/app/app.routes.ts`, `frontend/src/app/layout/shell/shell.component.ts`
- Test: `frontend/src/app/core/auth/permission.guard.spec.ts`, `frontend/src/app/app.routes.spec.ts` (nowy), `frontend/src/app/layout/shell/shell.component.spec.ts`

**Interfaces:**
- Consumes: `AuthService.hasPermission`, `Permissions` (Task 1).
- Produces: `permissionGuard(permission: string): CanActivateFn` (brak uprawnienia → `UrlTree` na `/dashboard`); `NavItem { label; icon; path; permission?: string }`.

- [ ] **Step 1: Testy (czerwone)**

`permission.guard.spec.ts`:

```ts
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
```

`app.routes.spec.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { routes } from './app.routes';
import { NAV_ITEMS } from './layout/nav-items';

describe('routes', () => {
  const children = routes.find(r => r.path === '')!.children!;

  it('protects every menu item that requires a permission with a canActivate guard', () => {
    const restricted = NAV_ITEMS.filter(item => item.permission);

    expect(restricted.length).toBeGreaterThan(0);
    for (const item of restricted) {
      const route = children.find(r => r.path === item.path.replace(/^\//, ''));
      expect(route, `brak trasy dla ${item.path}`).toBeDefined();
      expect(route!.canActivate?.length, `brak guarda na ${item.path}`).toBe(1);
    }
  });

  it('does not guard routes that are open to every authenticated user', () => {
    for (const item of NAV_ITEMS.filter(i => !i.permission)) {
      const route = children.find(r => r.path === item.path.replace(/^\//, ''))!;
      expect(route.canActivate).toBeUndefined();
    }
  });
});
```

Zastąp `shell.component.spec.ts` całością:

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect, vi } from 'vitest';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/auth/auth.service';

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;

  function setup(permissions: string[]) {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            roles: () => [],
            hasPermission: (p: string) => permissions.includes(p),
            logout: vi.fn()
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  }

  it('hides the admin nav items for a user without the matching permissions', () => {
    setup(['Meetings.View']);
    const text = fixture.nativeElement.textContent as string;

    expect(text).not.toContain('Użytkownicy i role');
    expect(text).not.toContain('Audit log');
  });

  it('shows the admin nav item for a user with Users.Manage', () => {
    setup(['Users.Manage']);

    expect((fixture.nativeElement.textContent as string)).toContain('Użytkownicy i role');
  });

  it('always shows the items that need no permission and only the permitted modules', () => {
    setup(['Meetings.View']);
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Baza osób');
    expect(text).toContain('Harmonogram i obecności');
    expect(text).not.toContain('Budżet SKŚP');
  });
});
```

- [ ] **Step 2: Uruchom — czerwone** (`permission.guard`, `item.permission` nie istnieją).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|Could not resolve|passed|failed" | head -6
```

- [ ] **Step 3: Implementacja**

`permission.guard.ts`:

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const permissionGuard = (permission: string): CanActivateFn => () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.hasPermission(permission) ? true : router.parseUrl('/dashboard');
};
```

`nav-items.ts` — zastąp całą zawartość:

```ts
import { Permissions } from '../core/auth/permissions';

export interface NavItem {
  label: string;
  icon: string;
  path: string;
  permission?: string;
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '◫', path: '/dashboard' },
  { label: 'Baza osób', icon: '◎', path: '/people' },
  { label: 'Kalendarz imienin', icon: '✿', path: '/name-days' },
  { label: 'Dokumenty i pisma', icon: '✎', path: '/documents', permission: Permissions.DocumentsView },
  { label: 'Mailing', icon: '✉', path: '/mailing', permission: Permissions.MailingView },
  { label: 'Kandydaci SKŚP', icon: '◉', path: '/candidates', permission: Permissions.CandidatesView },
  { label: 'Katechiści posłani', icon: '✦', path: '/missions', permission: Permissions.MissionsView },
  { label: 'Formatorzy', icon: '♙', path: '/formators', permission: Permissions.FormatorsView },
  { label: 'Parafie i giełda', icon: '⌂', path: '/parish-board', permission: Permissions.ParishNeedsView },
  { label: 'Rejestr parafii', icon: '✚', path: '/parishes', permission: Permissions.ParishesManage },
  { label: 'Budżet SKŚP', icon: '◈', path: '/budget/sksp', permission: Permissions.BudgetSkspView },
  { label: 'Podopieczni DOK', icon: '◍', path: '/dok-cases', permission: Permissions.DokCasesView },
  { label: 'Harmonogram i obecności', icon: '▦', path: '/meetings', permission: Permissions.MeetingsView },
  { label: 'Superwizje', icon: '◌', path: '/supervisions', permission: Permissions.SupervisionsView },
  { label: 'Absolwenci', icon: '✓', path: '/graduates', permission: Permissions.GraduatesView },
  { label: 'Budżet DOK', icon: '◈', path: '/budget/dok', permission: Permissions.BudgetDokView },
  { label: 'Użytkownicy i role', icon: '⚙', path: '/admin/users', permission: Permissions.UsersManage },
  { label: 'Audit log', icon: '⛨', path: '/audit-log', permission: Permissions.AuditLogView }
];
```

`shell.component.ts` — zamień `visibleNavItems`:

```ts
  readonly visibleNavItems = computed(() =>
    NAV_ITEMS.filter(item => !item.permission || this.auth.hasPermission(item.permission))
  );
```

`app.routes.ts` — zastąp całą zawartość:

```ts
import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { Permissions } from './core/auth/permissions';
import { ShellComponent } from './layout/shell/shell.component';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'people',
        loadComponent: () => import('./features/people/people-list.component').then(m => m.PeopleListComponent)
      },
      {
        path: 'name-days',
        loadComponent: () => import('./features/name-days/name-days-list.component').then(m => m.NameDaysListComponent)
      },
      {
        path: 'documents',
        canActivate: [permissionGuard(Permissions.DocumentsView)],
        loadComponent: () => import('./features/documents/documents.component').then(m => m.DocumentsComponent)
      },
      {
        path: 'mailing',
        canActivate: [permissionGuard(Permissions.MailingView)],
        loadComponent: () => import('./features/mailing/mailing.component').then(m => m.MailingComponent)
      },
      {
        path: 'candidates',
        canActivate: [permissionGuard(Permissions.CandidatesView)],
        loadComponent: () => import('./features/candidates/candidates-list.component').then(m => m.CandidatesListComponent)
      },
      {
        path: 'missions',
        canActivate: [permissionGuard(Permissions.MissionsView)],
        loadComponent: () => import('./features/missions/missions-list.component').then(m => m.MissionsListComponent)
      },
      {
        path: 'formators',
        canActivate: [permissionGuard(Permissions.FormatorsView)],
        loadComponent: () => import('./features/formators/formators-list.component').then(m => m.FormatorsListComponent)
      },
      {
        path: 'parish-board',
        canActivate: [permissionGuard(Permissions.ParishNeedsView)],
        loadComponent: () => import('./features/parish-board/parish-board.component').then(m => m.ParishBoardComponent)
      },
      {
        path: 'parishes',
        canActivate: [permissionGuard(Permissions.ParishesManage)],
        loadComponent: () => import('./features/parish-board/parishes-list.component').then(m => m.ParishesListComponent)
      },
      {
        path: 'budget/sksp',
        canActivate: [permissionGuard(Permissions.BudgetSkspView)],
        loadComponent: () => import('./features/budget/budget.component').then(m => m.BudgetComponent)
      },
      {
        path: 'dok-cases',
        canActivate: [permissionGuard(Permissions.DokCasesView)],
        loadComponent: () => import('./features/dok-cases/dok-cases-list.component').then(m => m.DokCasesListComponent)
      },
      {
        path: 'meetings',
        canActivate: [permissionGuard(Permissions.MeetingsView)],
        loadComponent: () => import('./features/meetings/meetings-list.component').then(m => m.MeetingsListComponent)
      },
      {
        path: 'supervisions',
        canActivate: [permissionGuard(Permissions.SupervisionsView)],
        loadComponent: () => import('./features/supervisions/supervisions-list.component').then(m => m.SupervisionsListComponent)
      },
      {
        path: 'graduates',
        canActivate: [permissionGuard(Permissions.GraduatesView)],
        loadComponent: () => import('./features/graduates/graduates-list.component').then(m => m.GraduatesListComponent)
      },
      {
        path: 'budget/dok',
        canActivate: [permissionGuard(Permissions.BudgetDokView)],
        loadComponent: () => import('./features/budget-dok/budget-dok.component').then(m => m.BudgetDokComponent)
      },
      {
        path: 'admin/users',
        canActivate: [permissionGuard(Permissions.UsersManage)],
        loadComponent: () => import('./features/admin-users/users-list.component').then(m => m.UsersListComponent)
      },
      {
        path: 'audit-log',
        canActivate: [permissionGuard(Permissions.AuditLogView)],
        loadComponent: () => import('./features/audit-log/audit-log.component').then(m => m.AuditLogComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -6
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/core/auth/permission.guard.ts frontend/src/app/core/auth/permission.guard.spec.ts frontend/src/app/layout/nav-items.ts frontend/src/app/app.routes.ts frontend/src/app/app.routes.spec.ts frontend/src/app/layout/shell/shell.component.ts frontend/src/app/layout/shell/shell.component.spec.ts
git commit -m "$(cat <<'EOF'
Frontend: menu i trasy sterowane uprawnieniami (permissionGuard)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Przycisk eksportu na uprawnieniach

**Files:**
- Modify: `frontend/src/app/shared/export/export-lists.ts`, `frontend/src/app/shared/export/export-button.component.ts`
- Test: `frontend/src/app/shared/export/export-button.component.spec.ts`

**Interfaces:**
- Consumes: `AuthService.hasPermission`, `Permissions` (Task 1).
- Produces: `ExportListConfig { path; fileName; permission: string }` (pole `roles` znika).

- [ ] **Step 1: Test (czerwony)** — w `export-button.component.spec.ts` zmień mock `AuthService` z `{ hasAnyRole: () => allowed }` na `{ hasPermission: () => allowed }`:

```ts
        { provide: AuthService, useValue: { hasPermission: () => allowed } }
```
oraz dopisz test:

```ts
  it('asks AuthService for the export permission of the list', () => {
    const hasPermission = vi.fn(() => true);
    TestBed.configureTestingModule({
      imports: [ExportButtonComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { hasPermission } }
      ]
    });
    const spyFixture = TestBed.createComponent(ExportButtonComponent);
    spyFixture.componentRef.setInput('list', 'dok-cases');
    spyFixture.detectChanges();

    expect(hasPermission).toHaveBeenCalledWith('DokCases.Export');
  });
```

- [ ] **Step 2: Uruchom — czerwone** (komponent woła jeszcze `hasAnyRole`, którego mock nie ma → `TypeError`).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "FAIL|TypeError|passed|failed" | head -5
```

- [ ] **Step 3: Implementacja** — `export-lists.ts` zastąp całością:

```ts
import { Permissions } from '../../core/auth/permissions';

export type ExportListKey =
  | 'people' | 'dok-cases' | 'candidates' | 'missions' | 'formators' | 'supervisions' | 'meetings' | 'parishes';

export interface ExportListConfig {
  path: string;
  fileName: string;
  permission: string;
}

export const EXPORT_LISTS: Record<ExportListKey, ExportListConfig> = {
  people: { path: 'people', fileName: 'osoby.xlsx', permission: Permissions.PeopleExport },
  'dok-cases': { path: 'dok-cases', fileName: 'podopieczni-dok.xlsx', permission: Permissions.DokCasesExport },
  candidates: { path: 'candidates', fileName: 'kandydaci-sksp.xlsx', permission: Permissions.CandidatesExport },
  missions: { path: 'missions', fileName: 'katechisci-poslani.xlsx', permission: Permissions.MissionsExport },
  formators: { path: 'formators', fileName: 'formatorzy.xlsx', permission: Permissions.FormatorsExport },
  supervisions: { path: 'supervisions', fileName: 'superwizje.xlsx', permission: Permissions.SupervisionsExport },
  meetings: { path: 'meetings', fileName: 'spotkania.xlsx', permission: Permissions.MeetingsExport },
  parishes: { path: 'parishes', fileName: 'parafie.xlsx', permission: Permissions.ParishesExport }
};
```

W `export-button.component.ts` zamień `canExport()`:

```ts
  canExport(): boolean {
    return this.auth.hasPermission(EXPORT_LISTS[this.list].permission);
  }
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -6
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/shared/export
git commit -m "$(cat <<'EOF'
Frontend: przycisk eksportu sterowany uprawnieniami

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Ukrycie przycisków akcji — Osoby, Kandydaci, Misje, Formatorzy, Spotkania, Superwizje

**Files (modify):** `people/people-list`, `candidates/candidates-list`, `missions/missions-list`, `formators/formators-list`, `meetings/meetings-list`, `supervisions/supervisions-list` — każdy `.component.ts` i `.component.html` w `frontend/src/app/features/<katalog>/`.
**Test (create):** `frontend/src/app/features/permission-wiring.spec.ts`

**Interfaces:**
- Consumes: `HasPermissionDirective` (Task 2; import `../../shared/permissions/has-permission.directive`), `AuthService.hasPermission` (Task 1).
- Produces: przyciski „Dodaj…”, „Edytuj”, „Usuń” widoczne tylko z odpowiednim `*.Manage`. Plik `permission-wiring.spec.ts` z tablicą `HEADER_CASES` (rozszerzana w Task 6).

- [ ] **Step 1: Test (czerwony)** — `permission-wiring.spec.ts`:

```ts
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { AuthService } from '../core/auth/auth.service';
import { environment } from '../../environments/environment';
import { PeopleListComponent } from './people/people-list.component';
import { CandidatesListComponent } from './candidates/candidates-list.component';
import { MissionsListComponent } from './missions/missions-list.component';
import { FormatorsListComponent } from './formators/formators-list.component';
import { MeetingsListComponent } from './meetings/meetings-list.component';
import { SupervisionsListComponent } from './supervisions/supervisions-list.component';

interface HeaderCase {
  name: string;
  component: Type<unknown>;
  permission: string;
  label: string;
}

const HEADER_CASES: HeaderCase[] = [
  { name: 'people', component: PeopleListComponent, permission: 'People.Manage', label: '＋ Dodaj osobę' },
  { name: 'candidates', component: CandidatesListComponent, permission: 'Candidates.Manage', label: '＋ Nowy kandydat' },
  { name: 'missions', component: MissionsListComponent, permission: 'Missions.Manage', label: '＋ Dodaj misję' },
  { name: 'formators', component: FormatorsListComponent, permission: 'Formators.Manage', label: '＋ Dodaj formatora' },
  { name: 'meetings', component: MeetingsListComponent, permission: 'Meetings.Manage', label: '＋ Dodaj spotkanie' },
  { name: 'supervisions', component: SupervisionsListComponent, permission: 'Supervisions.Manage', label: '＋ Nowa superwizja' }
];

function render(component: Type<unknown>, granted: string[]) {
  TestBed.configureTestingModule({
    imports: [component],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: { hasPermission: (p: string) => granted.includes(p), roles: () => [] } }
    ]
  });
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  return fixture;
}

describe('action buttons are gated by permissions', () => {
  for (const c of HEADER_CASES) {
    it(`${c.name}: header button is hidden without ${c.permission}`, () => {
      const fixture = render(c.component, []);

      expect(fixture.nativeElement.textContent as string).not.toContain(c.label);
    });

    it(`${c.name}: header button is visible with ${c.permission}`, () => {
      const fixture = render(c.component, [c.permission]);

      expect(fixture.nativeElement.textContent as string).toContain(c.label);
    });
  }

  it('people: row actions Edytuj/Usuń follow People.Manage', () => {
    const person = { id: '1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null };
    for (const [granted, expected] of [[[], false], [['People.Manage'], true]] as const) {
      TestBed.resetTestingModule();
      const fixture = render(PeopleListComponent, [...granted]);
      TestBed.inject(HttpTestingController)
        .expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`)
        .flush({ items: [person], totalCount: 1, page: 1, pageSize: 20 });
      fixture.detectChanges();

      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Anna Maj');
      expect(text.includes('Edytuj')).toBe(expected);
      expect(text.includes('Usuń')).toBe(expected);
    }
  });
});
```

- [ ] **Step 2: Uruchom — czerwone** (przyciski nie są jeszcze ukrywane — przypadki „hidden” padają).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "FAIL|passed|failed" | head -8
```

- [ ] **Step 3: Importy w komponentach** (z `C:\eu02_install\DOKPortalLight\frontend\src\app\features`):

```bash
cd C:/eu02_install/DOKPortalLight/frontend/src/app/features
for f in people/people-list candidates/candidates-list missions/missions-list formators/formators-list meetings/meetings-list supervisions/supervisions-list; do
  sed -i "1i import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';" $f.component.ts
  sed -i "s/imports: \[/imports: [HasPermissionDirective, /" $f.component.ts
  grep -n "HasPermissionDirective" $f.component.ts
done
```
Expected: dla każdego pliku dwie linie (import i tablica `imports`).

- [ ] **Step 4: Szablony** — pomocnik, który dopisuje dyrektywę do znacznika otwierającego i zgłasza brak dopasowania:

```bash
cd C:/eu02_install/DOKPortalLight/frontend/src/app/features
gate() {
  local file=$1 perm=$2 tag=$3
  local name=${tag%% *} rest=${tag#* }
  local new="$name *appHasPermission=\"'$perm'\" $rest"
  if ! grep -qF -- "$tag" "$file"; then echo "BRAK DOPASOWANIA: $file :: $tag"; return 1; fi
  sed -i "s|${tag}|${new}|" "$file"
}
gate people/people-list.component.html           People.Manage      '<button class="btn primary" (click)="openAddForm()">'
gate candidates/candidates-list.component.html   Candidates.Manage  '<button class="btn primary" (click)="openAddForm()">'
gate candidates/candidates-list.component.html   Candidates.Manage  '<span class="link" style="color:var(--danger)" (click)="deleteCandidate(candidate)">'
gate missions/missions-list.component.html       Missions.Manage    '<button class="btn primary" (click)="openAddForm()">'
gate missions/missions-list.component.html       Missions.Manage    '<span class="link" style="color:var(--danger)" (click)="deleteMission(mission)">'
gate formators/formators-list.component.html     Formators.Manage   '<button class="btn primary" (click)="openAddForm()">'
gate meetings/meetings-list.component.html       Meetings.Manage    '<button class="btn primary" (click)="openAddForm()">'
gate supervisions/supervisions-list.component.html Supervisions.Manage '<button class="btn primary" (click)="openAddForm()">'
grep -n "appHasPermission" */*list.component.html
```
Expected: 8 dopasowań, brak komunikatów „BRAK DOPASOWANIA”.

Grupy „Edytuj · Usuń” opakuj w `<ng-container>` (narzędzie Edit, dokładne teksty):

`people/people-list.component.html` — zamień
```html
              <span class="link" (click)="openEditForm(person)">Edytuj</span>
              &nbsp;·&nbsp;
              <span class="link" style="color:var(--danger)" (click)="deletePerson(person)">Usuń</span>
```
na
```html
              <ng-container *appHasPermission="'People.Manage'">
                <span class="link" (click)="openEditForm(person)">Edytuj</span>
                &nbsp;·&nbsp;
                <span class="link" style="color:var(--danger)" (click)="deletePerson(person)">Usuń</span>
              </ng-container>
```

`formators/formators-list.component.html` — zamień
```html
              <span class="link" (click)="openEditForm(formator)">Edytuj</span>
              &nbsp;·&nbsp;
              <span class="link" style="color:var(--danger)" (click)="deleteFormator(formator)">Usuń</span>
```
na
```html
              <ng-container *appHasPermission="'Formators.Manage'">
                <span class="link" (click)="openEditForm(formator)">Edytuj</span>
                &nbsp;·&nbsp;
                <span class="link" style="color:var(--danger)" (click)="deleteFormator(formator)">Usuń</span>
              </ng-container>
```

`meetings/meetings-list.component.html` — zamień
```html
              <span class="link" (click)="openEditForm(meeting)">Edytuj</span>
              &nbsp;·&nbsp;
              <span class="link" style="color:var(--danger)" (click)="deleteMeeting(meeting)">Usuń</span>
```
na
```html
              <ng-container *appHasPermission="'Meetings.Manage'">
                <span class="link" (click)="openEditForm(meeting)">Edytuj</span>
                &nbsp;·&nbsp;
                <span class="link" style="color:var(--danger)" (click)="deleteMeeting(meeting)">Usuń</span>
              </ng-container>
```

`supervisions/supervisions-list.component.html` — zamień
```html
        <span class="link" (click)="openEditForm(supervision)">Edytuj</span>
        &nbsp;·&nbsp;
        <span class="link" style="color:var(--danger)" (click)="deleteSupervision(supervision)">Usuń</span>
```
na
```html
        <ng-container *appHasPermission="'Supervisions.Manage'">
          <span class="link" (click)="openEditForm(supervision)">Edytuj</span>
          &nbsp;·&nbsp;
          <span class="link" style="color:var(--danger)" (click)="deleteSupervision(supervision)">Usuń</span>
        </ng-container>
```

- [ ] **Step 5: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -6
```
Jeśli któryś istniejący spec liczy na widoczność tych przycisków, zastąp w nim `AuthService` mockiem `{ hasPermission: () => true, roles: () => [] }`.

- [ ] **Step 6: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/people frontend/src/app/features/candidates frontend/src/app/features/missions frontend/src/app/features/formators frontend/src/app/features/meetings frontend/src/app/features/supervisions frontend/src/app/features/permission-wiring.spec.ts
git status --short
git commit -m "$(cat <<'EOF'
Frontend: ukryj przyciski akcji bez uprawnień (Osoby, Kandydaci, Misje, Formatorzy, Spotkania, Superwizje)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Ukrycie przycisków akcji — Podopieczni DOK, Parafie i giełda, Budżety, Mailing, Dokumenty + wpis „Co nowego”

**Files (modify):** `dok-cases/dok-cases-list` (`.ts`, `.html`, `.scss`), `parish-board/parish-board` (`.ts`, `.html`), `budget/budget` (`.ts`, `.html`), `budget-dok/budget-dok` (`.ts`, `.html`), `mailing/mailing` (`.ts`, `.html`), `documents/documents` (`.ts`, `.html`) — wszystko w `frontend/src/app/features/<katalog>/`; `dashboard/dashboard.component.ts` (changelog).
**Test (modify):** `frontend/src/app/features/permission-wiring.spec.ts`

**Interfaces:**
- Consumes: `HasPermissionDirective` (Task 2), `gate` z Task 5 (zdefiniuj ponownie w powłoce).
- Produces: przyciski akcji tych ekranów widoczne tylko z odpowiednimi uprawnieniami; wiersz akcji spraw DOK renderuje „Notatki”, „Dokumenty”, „Usuń” niezależnie (`PastoralNotes.View`, `CaseDocuments.View`, `DokCases.Manage`).

- [ ] **Step 1: Testy (czerwone)** — w `permission-wiring.spec.ts` dopisz importy:

```ts
import { ParishBoardComponent } from './parish-board/parish-board.component';
import { BudgetComponent } from './budget/budget.component';
import { BudgetDokComponent } from './budget-dok/budget-dok.component';
import { DokCasesListComponent } from './dok-cases/dok-cases-list.component';
import { MailingComponent } from './mailing/mailing.component';
import { DocumentsComponent } from './documents/documents.component';
```
dopisz do tablicy `HEADER_CASES`:

```ts
  { name: 'parish-board', component: ParishBoardComponent, permission: 'ParishNeeds.Manage', label: '＋ Nowe zapotrzebowanie' },
  { name: 'budget-sksp', component: BudgetComponent, permission: 'BudgetSksp.Manage', label: '＋ Dodaj operację' },
  { name: 'budget-dok', component: BudgetDokComponent, permission: 'BudgetDok.Manage', label: '＋ Dodaj operację' },
  { name: 'dok-cases', component: DokCasesListComponent, permission: 'DokCases.Manage', label: '＋ Nowy podopieczny' },
  { name: 'mailing', component: MailingComponent, permission: 'Mailing.Manage', label: '＋ Nowa kampania' },
  { name: 'documents', component: DocumentsComponent, permission: 'Documents.Generate', label: 'Generuj PDF' }
```
(zamień poprzednie zamykające `]` tablicy tak, by ostatni dotychczasowy element dostał przecinek) oraz dopisz w `describe` test wierszy spraw DOK:

```ts
  it('dok-cases: row actions are gated independently', () => {
    const dokCase = { id: '1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null };
    const scenarios: Array<[string[], boolean, boolean, boolean]> = [
      [[], false, false, false],
      [['PastoralNotes.View'], true, false, false],
      [['CaseDocuments.View'], false, true, false],
      [['DokCases.Manage'], false, false, true]
    ];
    for (const [granted, notes, documents, remove] of scenarios) {
      TestBed.resetTestingModule();
      const fixture = render(DokCasesListComponent, granted);
      const http = TestBed.inject(HttpTestingController);
      for (const req of http.match(r => r.url === `${environment.apiBaseUrl}/api/dok-cases`)) {
        req.flush({ items: [dokCase], totalCount: 1, page: 1, pageSize: 20 });
      }
      http.match(r => r.url === `${environment.apiBaseUrl}/api/people`)
        .forEach(req => req.flush({ items: [], totalCount: 0, page: 1, pageSize: 200 }));
      fixture.detectChanges();

      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Jan Kowalski');
      expect(text.includes('Notatki')).toBe(notes);
      expect(text.includes('Dokumenty')).toBe(documents);
      expect(text.includes('Usuń')).toBe(remove);
    }
  });
```

- [ ] **Step 2: Uruchom — czerwone** (nowe przypadki „hidden” i wiersze DOK padają).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "FAIL|passed|failed" | head -8
```

- [ ] **Step 3: Importy w komponentach**

```bash
cd C:/eu02_install/DOKPortalLight/frontend/src/app/features
for f in dok-cases/dok-cases-list parish-board/parish-board budget/budget budget-dok/budget-dok mailing/mailing documents/documents; do
  sed -i "1i import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';" $f.component.ts
  sed -i "s/imports: \[/imports: [HasPermissionDirective, /" $f.component.ts
  grep -n "HasPermissionDirective" $f.component.ts
done
```

- [ ] **Step 4: Szablony — jednoliniowe znaczniki**

```bash
cd C:/eu02_install/DOKPortalLight/frontend/src/app/features
gate() {
  local file=$1 perm=$2 tag=$3
  local name=${tag%% *} rest=${tag#* }
  local new="$name *appHasPermission=\"'$perm'\" $rest"
  if ! grep -qF -- "$tag" "$file"; then echo "BRAK DOPASOWANIA: $file :: $tag"; return 1; fi
  sed -i "s|${tag}|${new}|" "$file"
}
gate dok-cases/dok-cases-list.component.html   DokCases.Manage        '<button class="btn primary" (click)="openAddForm()">'
gate dok-cases/dok-cases-list.component.html   PastoralNotes.Write    '<button class="btn primary" (click)="addNote()">'
gate dok-cases/dok-cases-list.component.html   CaseDocuments.Manage   '<label class="btn ghost" style="cursor:pointer">'
gate dok-cases/dok-cases-list.component.html   CaseDocuments.Manage   '<button class="btn primary" (click)="addDocument()">'
gate parish-board/parish-board.component.html  ParishNeeds.Manage     '<button class="btn primary" (click)="openAddForm()">'
gate parish-board/parish-board.component.html  ParishNeeds.Manage     '<button class="btn primary small" (click)="openAssignForm(need)">'
gate parish-board/parish-board.component.html  ParishNeeds.Manage     '<span class="link" style="color:var(--danger)" (click)="deleteNeed(need)">'
gate budget/budget.component.html              BudgetSksp.Manage      '<button class="btn primary" (click)="openAddForm()">'
gate budget/budget.component.html              BudgetSksp.Manage      '<span class="link" style="color:var(--danger)" (click)="deleteEntry(entry)">'
gate budget-dok/budget-dok.component.html      BudgetDok.Manage       '<button class="btn primary" (click)="openAddForm()">'
gate budget-dok/budget-dok.component.html      BudgetDok.Manage       '<span class="link" style="color:var(--danger)" (click)="deleteEntry(entry)">'
gate mailing/mailing.component.html            Mailing.Manage         '<button class="btn primary" (click)="openAddForm()">'
gate mailing/mailing.component.html            Mailing.Manage         '<button class="btn primary small" (click)="sendCampaign(campaign)">'
grep -c "appHasPermission" dok-cases/dok-cases-list.component.html parish-board/parish-board.component.html budget/budget.component.html budget-dok/budget-dok.component.html mailing/mailing.component.html
```
Expected: 4, 3, 2, 2, 2 dopasowań i brak komunikatów „BRAK DOPASOWANIA”.

- [ ] **Step 5: Szablony — fragmenty wieloliniowe** (narzędzie Edit, dokładne teksty)

`dok-cases/dok-cases-list.component.html` — wiersz akcji: zamień
```html
            <td>
              <span class="link" (click)="openNotes(dokCase)">Notatki</span>
              &nbsp;·&nbsp;
              <span class="link" (click)="openDocuments(dokCase)">Dokumenty</span>
              &nbsp;·&nbsp;
              <span class="link" style="color:var(--danger)" (click)="deleteCase(dokCase)">Usuń</span>
            </td>
```
na
```html
            <td>
              <span class="row-actions">
                <span *appHasPermission="'PastoralNotes.View'" class="link" (click)="openNotes(dokCase)">Notatki</span>
                <span *appHasPermission="'CaseDocuments.View'" class="link" (click)="openDocuments(dokCase)">Dokumenty</span>
                <span *appHasPermission="'DokCases.Manage'" class="link" style="color:var(--danger)" (click)="deleteCase(dokCase)">Usuń</span>
              </span>
            </td>
```

`dok-cases/dok-cases-list.component.html` — pole nowej notatki: zamień
```html
        <div class="field full">
          <label>Nowa notatka</label>
```
na
```html
        <div *appHasPermission="'PastoralNotes.Write'" class="field full">
          <label>Nowa notatka</label>
```

`dok-cases/dok-cases-list.component.html` — pole nowej pozycji dokumentu: zamień
```html
        <div class="field full">
          <label>Nowa pozycja (nazwa dokumentu)</label>
```
na
```html
        <div *appHasPermission="'CaseDocuments.Manage'" class="field full">
          <label>Nowa pozycja (nazwa dokumentu)</label>
```

`dok-cases/dok-cases-list.component.scss` — dopisz (plik jest pusty):
```scss
.row-actions {
  display: inline-flex;
  gap: 14px;
}
```

`documents/documents.component.html` — zamień pierwszą kartę generatora
```html
<div class="card">
  <div class="card-body">
    <div class="form-grid">
      <div class="field">
        <label>Szablon</label>
```
na
```html
<div *appHasPermission="'Documents.Generate'" class="card">
  <div class="card-body">
    <div class="form-grid">
      <div class="field">
        <label>Szablon</label>
```

- [ ] **Step 6: Wpis „Co nowego”** — w `frontend/src/app/features/dashboard/dashboard.component.ts` dodaj na początku tablicy `changelog`:

```ts
    { date: '2026-10-02', text: 'Uprawnienia — menu, ekrany i przyciski (Dodaj, Edytuj, Usuń, Eksport) pokazują się teraz zależnie od uprawnień przypisanych Twojej roli. Po wdrożeniu trzeba zalogować się ponownie. Zaostrzono też dostęp do odczytu: np. budżety, kandydaci SKŚP czy sprawy DOK widzą tylko role, które ich potrzebują.' },
```

- [ ] **Step 7: Uruchom — zielone, plus build**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -6; npx ng build 2>&1 | grep -iE "error|Application bundle" | head -4
```
Expected: wszystkie testy przechodzą, build bez błędów. Jeśli któryś istniejący spec liczy na widoczność przycisku, zastąp w nim `AuthService` mockiem `{ hasPermission: () => true, roles: () => [] }`.

- [ ] **Step 8: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/dok-cases frontend/src/app/features/parish-board frontend/src/app/features/budget frontend/src/app/features/budget-dok frontend/src/app/features/mailing frontend/src/app/features/documents frontend/src/app/features/dashboard/dashboard.component.ts frontend/src/app/features/permission-wiring.spec.ts docs/superpowers/plans/2026-10-02-permissions-part2-frontend.md
git status --short
git commit -m "$(cat <<'EOF'
Frontend: ukryj przyciski akcji bez uprawnień (Sprawy DOK, giełda, budżety, mailing, dokumenty)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

## Self-review

- **Pokrycie specyfikacji (część 2):** `AuthService.permissions/hasPermission/hasAnyPermission` + obsługa pojedynczego claima (string) i tablicy — Task 1; stare tokeny (flaga) — Task 1; `nav-items.ts` z `permission` — Task 3; `export-lists.ts` + `ExportButtonComponent` — Task 4; `permissionGuard` na trasach — Task 3; ukrycie przycisków Dodaj/Edytuj/Usuń/Wyślij/Skieruj/Generuj/Notatki/Dokumenty — Tasks 5–6; wpis „Co nowego” — Task 6.
- **Placeholdery:** brak; wszystkie zmiany szablonów mają dokładne teksty lub polecenia z weryfikacją `grep`.
- **Spójność typów:** `Permissions.*` (Task 1) użyte w `nav-items.ts`, `export-lists.ts`, `app.routes.ts`; `permissionGuard(permission: string)` (Task 3); `HasPermissionDirective` (Task 2) importowana w Tasks 5–6; pole `NavItem.permission?` używane w teście tras i powłoce; `ExportListConfig.permission` używane w przycisku.
- **Świadome pominięcia:** `parishes-list` (strona wymaga `Parishes.Manage` na trasie, więc przyciski nie wymagają osobnego ukrywania), `admin-users` i `audit-log` (strony chronione uprawnieniem na trasie), formularze modalne (otwierane przyciskami, które są już ukryte).
