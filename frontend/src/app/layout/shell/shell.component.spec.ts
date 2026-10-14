import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, it, expect, vi } from 'vitest';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/auth/auth.service';
import { IdleTimeoutService } from '../../core/auth/idle-timeout.service';

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;
  let logout: ReturnType<typeof vi.fn>;
  const idle = { start: vi.fn(), stop: vi.fn() };

  function setup(permissions: string[], roles: string[] = [], extra: Record<string, unknown> = {}) {
    logout = vi.fn();
    idle.start.mockClear();
    idle.stop.mockClear();
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        { provide: IdleTimeoutService, useValue: idle },
        {
          provide: AuthService,
          useValue: {
            roles: () => roles,
            hasPermission: (p: string) => permissions.includes(p),
            logout,
            ...extra
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  }

  afterEach(() => vi.restoreAllMocks());

  const el = () => fixture.nativeElement as HTMLElement;

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

  it('shows the footer with the copyright, application name and version under the content', () => {
    setup([]);
    const main = el().querySelector('main.main') as HTMLElement;
    const footer = main.querySelector('app-footer footer');

    expect(footer).not.toBeNull();
    expect(footer!.textContent).toContain('Diecezja Bydgoska');
    expect(footer!.textContent).toContain('DOK Portal · wersja');
    expect(main.lastElementChild!.tagName.toLowerCase()).toBe('app-footer');
  });

  it('shows the cross as the brand mark in the menu, not the letters SK', () => {
    setup([]);
    const logo = el().querySelector('.sidebar .brand .logo') as HTMLElement;

    expect(logo.querySelector('svg')).not.toBeNull();
    expect(logo.querySelectorAll('svg line')).toHaveLength(2);
    expect((logo.textContent ?? '').trim()).toBe('');
  });

  it('always shows the items that need no permission and only the permitted modules', () => {
    setup(['Meetings.View']);
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Dashboard');
    expect(text).toContain('Harmonogram i obecności');
    expect(text).not.toContain('Budżet SKŚP');
    // Osoby i imieniny to dane osobowe – tylko z „Osoby — przeglądanie”.
    expect(text).not.toContain('Osoby');
    expect(text).not.toContain('Kalendarz imienin');
  });

  it('shows people and name days to accounts with access to people', () => {
    setup(['People.View']);
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Osoby');
    expect(text).toContain('Kalendarz imienin');
  });

  it('shows the roles of the signed-in user in the top bar', () => {
    setup([], ['DyrektorDOK', 'Superwizor']);

    const pills = Array.from(el().querySelectorAll('.top-actions .pill')).map(p => p.textContent!.trim());
    expect(pills).toEqual(['DyrektorDOK', 'Superwizor']);
  });

  it('signs the user out from the top bar', () => {
    setup([]);

    (el().querySelector('.top-actions button') as HTMLButtonElement).click();

    expect(logout).toHaveBeenCalledTimes(1);
  });

  it('starts the idle timer when shown and stops it when destroyed', () => {
    setup([]);
    expect(idle.start).toHaveBeenCalledTimes(1);

    fixture.destroy();

    expect(idle.stop).toHaveBeenCalledTimes(1);
  });

  describe('mobile menu', () => {
    const sidebar = () => el().querySelector('.sidebar') as HTMLElement;
    const backdrop = () => el().querySelector('.sidebar-backdrop') as HTMLElement;

    it('opens and closes the sidebar with the hamburger button', () => {
      setup([]);
      expect(sidebar().classList.contains('open')).toBe(false);

      (el().querySelector('.mobile-menu') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(sidebar().classList.contains('open')).toBe(true);
      expect(backdrop().classList.contains('show')).toBe(true);

      (el().querySelector('.mobile-menu') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(sidebar().classList.contains('open')).toBe(false);
    });

    it('closes the sidebar when the backdrop is clicked', () => {
      setup([]);
      fixture.componentInstance.toggleSidebar();
      fixture.detectChanges();

      backdrop().click();
      fixture.detectChanges();

      expect(sidebar().classList.contains('open')).toBe(false);
    });

    it('closes the sidebar after choosing a menu item', () => {
      setup([]);
      fixture.componentInstance.toggleSidebar();
      fixture.detectChanges();

      (el().querySelector('.nav-item') as HTMLElement).click();
      fixture.detectChanges();

      expect(sidebar().classList.contains('open')).toBe(false);
    });
  });

  it('suggests turning on MFA after login, with a link to the account page and a "later" button', async () => {
    const { signal } = await import('@angular/core');
    const suggestMfa = signal(true);
    const dismissMfaSuggestion = vi.fn(() => suggestMfa.set(false));
    setup([], [], { suggestMfa, mustChangePassword: () => false, dismissMfaSuggestion });

    const nudge = el().querySelector('.mfa-nudge')!;
    expect(nudge.textContent).toContain('Zabezpiecz swoje konto');
    expect(nudge.querySelector('a')!.getAttribute('href')).toBe('/konto/haslo#mfa');
    (nudge.querySelector('a') as HTMLAnchorElement).click();
    expect(dismissMfaSuggestion).toHaveBeenCalledTimes(1);
    suggestMfa.set(true);
    fixture.detectChanges();
    (Array.from(nudge.querySelectorAll('button')).find(b => b.textContent!.includes('Później')) as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(dismissMfaSuggestion).toHaveBeenCalledTimes(2);
    expect(el().querySelector('.mfa-nudge')).toBeNull();
  });

  it('does not nag while the password must still be changed', () => {
    setup([], [], { suggestMfa: () => true, mustChangePassword: () => true, dismissMfaSuggestion: vi.fn() });
    expect(el().querySelector('.mfa-nudge')).toBeNull();
  });
});
