import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/auth/auth.service';
import { click, setInput } from '../../testing/test-helpers';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceMock: { login: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    authServiceMock = { login: vi.fn() };

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: authServiceMock }]
    });

    fixture = TestBed.createComponent(LoginComponent);
  });

  it('navigates to /dashboard after a successful login', async () => {
    authServiceMock.login.mockResolvedValue(undefined);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const component = fixture.componentInstance;
    component.email = 'a@b.pl';
    component.password = 'secret';

    await component.submit();

    expect(authServiceMock.login).toHaveBeenCalledWith('a@b.pl', 'secret');
    expect(navigateSpy).toHaveBeenCalledWith('/dashboard');
  });

  it('shows an error message when login fails', async () => {
    authServiceMock.login.mockRejectedValue(new Error('unauthorized'));
    const component = fixture.componentInstance;

    await component.submit();

    expect(component.errorMessage()).toBe('Nieprawidłowy e-mail lub hasło.');
  });

  describe('in the rendered form', () => {
    afterEach(() => {
      vi.useRealTimers();
      history.replaceState(null, '');
    });

    function render() {
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('submits the typed credentials from the form and shows the error alert on failure', async () => {
      authServiceMock.login.mockRejectedValue(new Error('unauthorized'));
      const el = render();
      await fixture.whenStable();
      setInput(el, '#login-email', 'a@b.pl');
      setInput(el, '#login-password', 'zle-haslo');

      click(el, '.submit-btn');
      await fixture.whenStable();
      fixture.detectChanges();

      expect(authServiceMock.login).toHaveBeenCalledWith('a@b.pl', 'zle-haslo');
      expect(el.querySelector('[role="alert"]')?.textContent).toContain('Nieprawidłowy e-mail lub hasło.');
      expect((el.querySelector('.submit-btn') as HTMLButtonElement).disabled).toBe(false);
    });

    it('locks the form and ignores a second submit while the login is pending', async () => {
      let finish!: () => void;
      authServiceMock.login.mockReturnValue(new Promise<void>(resolve => (finish = resolve)));
      const el = render();
      const component = fixture.componentInstance;

      const first = component.submit();
      void component.submit();
      fixture.detectChanges();

      expect(authServiceMock.login).toHaveBeenCalledTimes(1);
      expect((el.querySelector('.submit-btn') as HTMLButtonElement).disabled).toBe(true);
      expect(el.textContent).toContain('Logowanie…');

      finish();
      await first;
      fixture.detectChanges();
      expect((el.querySelector('.submit-btn') as HTMLButtonElement).disabled).toBe(false);
    });

    it('explains that the server is waking up when the login takes longer than four seconds', async () => {
      vi.useFakeTimers();
      let finish!: () => void;
      authServiceMock.login.mockReturnValue(new Promise<void>(resolve => (finish = resolve)));
      const el = render();

      const pending = fixture.componentInstance.submit();
      vi.advanceTimersByTime(3999);
      fixture.detectChanges();
      expect(el.textContent).not.toContain('wybudza');

      vi.advanceTimersByTime(2);
      fixture.detectChanges();
      expect(el.textContent).toContain('Serwer właśnie się wybudza');

      finish();
      await pending;
      fixture.detectChanges();
      expect(el.textContent).not.toContain('Serwer właśnie się wybudza');
    });

    it('toggles the password visibility', () => {
      const el = render();
      const toggle = () => el.querySelector('button[aria-label$="hasło"]') as HTMLButtonElement;
      expect((el.querySelector('#login-password') as HTMLInputElement).type).toBe('password');
      expect(toggle().getAttribute('aria-label')).toBe('Pokaż hasło');

      toggle().click();
      fixture.detectChanges();

      expect((el.querySelector('#login-password') as HTMLInputElement).type).toBe('text');
      expect(toggle().getAttribute('aria-label')).toBe('Ukryj hasło');
    });

    it('tells the user the session expired after an idle logout', () => {
      history.replaceState({ reason: 'idle' }, '');
      const idleFixture = TestBed.createComponent(LoginComponent);
      idleFixture.detectChanges();

      expect(idleFixture.nativeElement.textContent).toContain('Sesja wygasła z powodu braku aktywności');
    });

    it('does not show the session note on a normal visit', () => {
      const el = render();

      expect(el.textContent).not.toContain('Sesja wygasła');
    });
  });
});
