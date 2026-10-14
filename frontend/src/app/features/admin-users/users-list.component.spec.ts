import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { UsersListComponent } from './users-list.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, click, clickByText, flushAll, paged, setInput, setup, textOf } from '../../testing/test-helpers';

const person = {
  id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski',
  email: 'jan@example.org', phone: null, birthDate: null, parishId: null, parishName: null, notes: null
};
const user = { id: 'u1', email: 'admin@dokportal.local', personId: null, personFullName: null, roles: ['Administrator'] };
const linkedUser = { id: 'u2', email: 'anna@example.org', personId: 'p9', personFullName: 'Anna Maj', roles: ['KatechistaProwadzacy'] };
const unlinkedCatechist = { id: 'u3', email: 'kat@example.org', personId: null, personFullName: null, roles: ['KatechistaProwadzacy'] };

function boot(users: object[] = [user]) {
  const ctx = setup(UsersListComponent);
  ctx.fixture.detectChanges();
  flushAll(ctx.http, api('/api/users'), users);
  flushAll(ctx.http, api('/api/users/roles'), ['Administrator', 'Biskup']);
  ctx.fixture.detectChanges();
  return ctx;
}

function bootCreate(users: object[] = [user]) {
  const ctx = boot(users);
  clickByText(ctx.el, '＋ Nowe konto', 'button');
  ctx.fixture.detectChanges();
  return ctx;
}

function openEdit(ctx: ReturnType<typeof boot>, index = 0) {
  (ctx.el.querySelectorAll<HTMLButtonElement>('.actions-cell button')[index]).click();
  ctx.fixture.detectChanges();
}

const save = (el: HTMLElement) => clickByText(el, 'Zapisz', '.modal-foot button');

function fillAccount(el: HTMLElement, email: string, password: string, confirm: string) {
  setInput(el, 'input[name="email"]', email);
  setInput(el, 'input[name="password"]', password);
  setInput(el, 'input[name="confirmPassword"]', confirm);
}

function toasts() {
  return TestBed.inject(ToastService).toasts();
}

describe('UsersListComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('the list', () => {
    it('shows login, linked person, roles in Polish and the account state, read-only', () => {
      const { el } = boot([user, { ...linkedUser, mfaEnabled: true }, { ...unlinkedCatechist, isActive: false }]);

      const headers = Array.from(el.querySelectorAll('thead th')).map(th => th.textContent!.trim());
      expect(headers).toEqual(['Login', 'Osoba', 'Role', 'Stan', '']);
      const text = textOf(el);
      expect(text).toContain('admin@dokportal.local');
      expect(text).toContain('Katechista prowadzący');
      expect(text).toContain('MFA');
      expect(text).toContain('wyłączone');
      expect(text).toContain('Katechista bez osoby nie zobaczy podopiecznych.');
      expect(el.querySelector('a.person-link')?.getAttribute('href')).toBe('/people/p9?z=uzytkownicy');
      expect(el.querySelectorAll('tbody input').length).toBe(0);
    });

    it('keeps custom role names as they are', () => {
      const { el } = boot([{ ...user, roles: ['Wlasna'] }]);
      expect(textOf(el)).toContain('Wlasna');
    });

    it('shows toasts when the user list or the role list cannot be loaded', () => {
      const ctx = setup(UsersListComponent);
      ctx.fixture.detectChanges();
      ctx.http.expectOne(api('/api/users')).flush('x', { status: 500, statusText: 'Error' });
      ctx.http.expectOne(api('/api/users/roles')).flush('x', { status: 500, statusText: 'Error' });

      expect(toasts().map(t => t.message)).toEqual(expect.arrayContaining(['Nie udało się wczytać listy użytkowników.', 'Nie udało się wczytać listy ról.']));
    });

    it('opens and closes the new account window', () => {
      const ctx = bootCreate();
      expect(textOf(ctx.el)).toContain('Nowe konto');
      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });

    it('gives the new account the roles ticked in the window', () => {
      const { el, http, fixture } = bootCreate();
      (el.querySelectorAll<HTMLInputElement>('.role-checks input')[1]).click();
      fixture.detectChanges();
      fillAccount(el, 'nowy@example.org', 'Sekret123!', 'Sekret123!');
      clickByText(el, 'Utwórz konto');

      expect(http.expectOne(r => r.method === 'POST' && r.url === api('/api/users')).request.body.roles).toEqual(['Biskup']);
    });
  });

  describe('creating an account', () => {
    it('rejects mismatched passwords without calling the API', () => {
      const { fixture, http, el } = bootCreate();
      fillAccount(el, 'nowy@example.org', 'Sekret123!', 'inne');

      clickByText(el, 'Utwórz konto');
      fixture.detectChanges();

      expect(textOf(el)).toContain('Podane hasła różnią się od siebie.');
      http.expectNone(r => r.method === 'POST');
    });

    it('creates an account for a person found through the debounced search', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = bootCreate();

      setInput(el, 'input[name="personQuery"]', 'Jan');
      http.expectNone(r => r.url === api('/api/people'));
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people') && r.params.get('query') === 'Jan').flush(paged([person]));
      fixture.detectChanges();
      clickByText(el, 'Jan Kowalski', 'button.list-row');
      fixture.detectChanges();
      expect(textOf(el)).toContain('jan@example.org');
      fillAccount(el, 'jan@example.org', 'Sekret123!', 'Sekret123!');
      clickByText(el, 'Utwórz konto');

      const req = http.expectOne(r => r.method === 'POST' && r.url === api('/api/users'));
      expect(req.request.body).toEqual({ email: 'jan@example.org', password: 'Sekret123!', roles: [], personId: 'p1' });
      req.flush({ id: 'u2', email: 'jan@example.org', personId: 'p1', roles: [] });
      flushAll(http, api('/api/users'), [user]);
      flushAll(http, api('/api/users/roles'), ['Administrator']);
      fixture.detectChanges();

      expect(toasts().map(t => t.message)).toContain('Konto użytkownika utworzone.');
      expect(fixture.componentInstance.newUser).toEqual({ email: '', password: '', roles: [] });
      expect(fixture.componentInstance.confirmPassword).toBe('');
      expect(fixture.componentInstance.isCreateOpen()).toBe(false);
    });

    function pickPerson(found: object) {
      vi.useFakeTimers();
      const ctx = bootCreate();
      setInput(ctx.el, 'input[name="personQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged([found]));
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Jan Kowalski', 'button.list-row');
      ctx.fixture.detectChanges();
      return ctx;
    }

    it('uses the person’s e-mail as the login, without typing it again', async () => {
      const { fixture, http, el } = pickPerson(person);
      await fixture.whenStable();
      fixture.detectChanges();

      const email = el.querySelector<HTMLInputElement>('input[name="email"]')!;
      expect(email.disabled).toBe(true);
      expect(email.value).toBe('jan@example.org');
      expect(textOf(el)).toContain('Login = e-mail osoby');
      setInput(el, 'input[name="password"]', 'Sekret123!');
      setInput(el, 'input[name="confirmPassword"]', 'Sekret123!');
      clickByText(el, 'Utwórz konto');

      expect(http.expectOne(r => r.method === 'POST' && r.url === api('/api/users')).request.body)
        .toEqual({ email: 'jan@example.org', password: 'Sekret123!', roles: [], personId: 'p1' });
    });

    it('asks for an e-mail when the person has none, and says it will be saved with the person', () => {
      const { fixture, http, el } = pickPerson({ ...person, email: null });

      expect(el.querySelector<HTMLInputElement>('input[name="email"]')!.disabled).toBe(false);
      expect(textOf(el)).toContain('zapiszemy też przy osobie');
      setInput(el, 'input[name="password"]', 'Sekret123!');
      setInput(el, 'input[name="confirmPassword"]', 'Sekret123!');
      clickByText(el, 'Utwórz konto');
      fixture.detectChanges();

      expect(textOf(el)).toContain('Podaj e-mail');
      http.expectNone(r => r.method === 'POST');
    });

    it('debounces the search: only the last query is sent and a blank query sends nothing', () => {
      vi.useFakeTimers();
      const { http, el } = bootCreate();

      setInput(el, 'input[name="personQuery"]', 'Ja');
      vi.advanceTimersByTime(100);
      setInput(el, 'input[name="personQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people') && r.params.get('query') === 'Jan').flush(paged([]));

      setInput(el, 'input[name="personQuery"]', '   ');
      vi.advanceTimersByTime(500);
      http.expectNone(r => r.url === api('/api/people'));
    });

    it('shows a toast when the person search fails', () => {
      vi.useFakeTimers();
      const { http, el } = bootCreate();

      setInput(el, 'input[name="personQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people')).flush('x', { status: 500, statusText: 'Server Error' });

      expect(toasts().map(t => t.message)).toContain('Nie udało się wyszukać osób.');
    });

    it('lets the user change the selected person', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = bootCreate();
      setInput(el, 'input[name="personQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people')).flush(paged([person]));
      fixture.detectChanges();
      clickByText(el, 'Jan Kowalski', 'button.list-row');
      fixture.detectChanges();

      clickByText(el, 'Zmień');
      fixture.detectChanges();

      expect(el.querySelector('input[name="personQuery"]')).not.toBeNull();
    });

    it('creates a new person first when adding an account for someone who is not in the database', () => {
      const { fixture, http, el } = bootCreate();
      clickByText(el, 'Dodaj nową osobę');
      fixture.detectChanges();
      fillAccount(el, 'ewa@example.org', 'Sekret123!', 'Sekret123!');

      clickByText(el, 'Utwórz konto');
      fixture.detectChanges();
      expect(textOf(el)).toContain('Podaj imię i nazwisko nowej osoby.');
      http.expectNone(r => r.method === 'POST');

      setInput(el, 'input[name="newPersonFirstName"]', ' Ewa ');
      setInput(el, 'input[name="newPersonLastName"]', 'Nowak');
      clickByText(el, 'Utwórz konto');
      const personReq = http.expectOne(r => r.method === 'POST' && r.url === api('/api/people'));
      expect(personReq.request.body).toEqual({ firstName: 'Ewa', lastName: 'Nowak', email: 'ewa@example.org' });
      personReq.flush({ ...person, id: 'new-1', firstName: 'Ewa', lastName: 'Nowak', fullName: 'Ewa Nowak' });

      const userReq = http.expectOne(r => r.method === 'POST' && r.url === api('/api/users'));
      expect(userReq.request.body.personId).toBe('new-1');
    });

    it('shows an error when the new person cannot be created, and allows cancelling', () => {
      const { fixture, http, el } = bootCreate();
      clickByText(el, 'Dodaj nową osobę');
      fixture.detectChanges();
      fillAccount(el, 'ewa@example.org', 'Sekret123!', 'Sekret123!');
      setInput(el, 'input[name="newPersonFirstName"]', 'Ewa');
      setInput(el, 'input[name="newPersonLastName"]', 'Nowak');

      clickByText(el, 'Utwórz konto');
      http.expectOne(r => r.method === 'POST' && r.url === api('/api/people'))
        .flush('x', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();
      expect(textOf(el)).toContain('Nie udało się utworzyć nowej osoby.');

      clickByText(el, 'Anuluj dodawanie nowej osoby');
      fixture.detectChanges();
      expect(el.querySelector('input[name="personQuery"]')).not.toBeNull();
    });

    it('shows an error when the account cannot be created', () => {
      const { fixture, http, el } = bootCreate();
      fillAccount(el, 'nowy@example.org', 'Sekret123!', 'Sekret123!');

      clickByText(el, 'Utwórz konto');
      http.expectOne(r => r.method === 'POST' && r.url === api('/api/users'))
        .flush('x', { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();

      expect(textOf(el)).toContain('Nie udało się utworzyć konta użytkownika.');
    });

    it('shows the server reason when the person or the account is refused', () => {
      const { fixture, http, el } = bootCreate();
      clickByText(el, 'Dodaj nową osobę');
      fixture.detectChanges();
      fillAccount(el, 'ewa@example.org', 'Sekret123!', 'Sekret123!');
      setInput(el, 'input[name="newPersonFirstName"]', 'Ewa');
      setInput(el, 'input[name="newPersonLastName"]', 'Nowak');

      clickByText(el, 'Utwórz konto');
      const person = http.expectOne(r => r.method === 'POST' && r.url === api('/api/people'));
      expect(person.request.body).toEqual({ firstName: 'Ewa', lastName: 'Nowak', email: 'ewa@example.org' });
      person.flush({ title: 'Ten adres e-mail ma już: Ewa Kowalska.' }, { status: 409, statusText: 'Conflict' });
      fixture.detectChanges();
      expect(textOf(el)).toContain('Ten adres e-mail ma już: Ewa Kowalska.');

      clickByText(el, 'Utwórz konto');
      http.expectOne(r => r.method === 'POST' && r.url === api('/api/people')).flush({ id: 'p7' });
      http.expectOne(r => r.method === 'POST' && r.url === api('/api/users'))
        .flush({ title: 'Nie udało się utworzyć użytkownika: hasło za krótkie' }, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();
      expect(textOf(el)).toContain('hasło za krótkie');
    });
  });

  describe('editing an account', () => {
    it('opens the account in a window with its login, person and roles', async () => {
      const ctx = boot([linkedUser]);
      openEdit(ctx);
      await ctx.fixture.whenStable();

      expect((ctx.el.querySelector('#edit-login') as HTMLInputElement).value).toBe('anna@example.org');
      expect(textOf(ctx.el)).toContain('Anna Maj');
      expect(textOf(ctx.el)).toContain('Zmiana loginu zmienia też e-mail powiązanej osoby.');
      clickByText(ctx.el, '×', '.modal .close');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });

    it('saves only what changed: login, then person, then roles', async () => {
      vi.useFakeTimers();
      const ctx = boot([linkedUser]);
      openEdit(ctx);
      setInput(ctx.el, '#edit-login', 'anna.maj@example.org');
      clickByText(ctx.el, 'Zmień', '.person-row .link');
      ctx.fixture.detectChanges();
      setInput(ctx.el, 'input[name="linkQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged([person]));
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Jan Kowalski', 'button.pick-row');
      ctx.fixture.detectChanges();
      (ctx.el.querySelector('input[name="role-Biskup"]') as HTMLInputElement).click();
      ctx.fixture.detectChanges();
      vi.useRealTimers();

      save(ctx.el);
      await Promise.resolve();
      ctx.http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u2/login')).flush({ ...linkedUser });
      await new Promise(r => setTimeout(r));
      const personReq = ctx.http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u2/person'));
      expect(personReq.request.body).toEqual({ personId: 'p1' });
      personReq.flush({ ...linkedUser });
      await new Promise(r => setTimeout(r));
      const rolesReq = ctx.http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u2/roles'));
      expect(rolesReq.request.body).toEqual({ roles: ['KatechistaProwadzacy', 'Biskup'] });
      rolesReq.flush({ ...linkedUser });
      await new Promise(r => setTimeout(r));

      expect(toasts().map(t => t.message)).toContain('Zapisano zmiany konta.');
      expect(ctx.fixture.componentInstance.editing()).toBeNull();
    });

    it('unlinks the person and reports refusals, keeping the window open', async () => {
      const ctx = boot([linkedUser]);
      openEdit(ctx);
      clickByText(ctx.el, 'Odepnij', '.person-row .link');
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Powiąż z osobą');

      save(ctx.el);
      await Promise.resolve();
      ctx.http.expectOne(r => r.url === api('/api/users/u2/person'))
        .flush({ title: 'To ostatni administrator.' }, { status: 400, statusText: 'Bad Request' });
      await new Promise(r => setTimeout(r));
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).toContain('To ostatni administrator.');
      expect(ctx.fixture.componentInstance.editing()).not.toBeNull();
    });

    it('needs a login, and sends nothing when nothing changed', async () => {
      const ctx = boot([linkedUser]);
      openEdit(ctx);
      setInput(ctx.el, '#edit-login', '  ');
      save(ctx.el);
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Podaj login');

      setInput(ctx.el, '#edit-login', 'anna@example.org');
      save(ctx.el);
      await new Promise(r => setTimeout(r));
      ctx.http.expectNone(r => r.method === 'PUT');
    });

    it('cancels picking a person', () => {
      vi.useFakeTimers();
      const ctx = boot([user]);
      openEdit(ctx);
      clickByText(ctx.el, 'Powiąż z osobą', '.person-row .link');
      ctx.fixture.detectChanges();
      setInput(ctx.el, 'input[name="linkQuery"]', '  ');
      vi.advanceTimersByTime(500);
      ctx.http.expectNone(r => r.url === api('/api/people'));
      setInput(ctx.el, 'input[name="linkQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      ctx.http.expectOne(r => r.url === api('/api/people')).flush('x', { status: 500, statusText: 'Error' });
      expect(toasts().map(t => t.message)).toContain('Nie udało się wyszukać osób.');
      clickByText(ctx.el, 'Anuluj wybór', 'button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('input[name="linkQuery"]')).toBeNull();
    });

    it('resets the password from the window', () => {
      const ctx = boot([user]);
      openEdit(ctx);
      clickByText(ctx.el, 'Resetuj hasło', 'button');
      ctx.fixture.detectChanges();
      setInput(ctx.el, 'input[name="resetPasswordValue"]', 'Tymczas123!');
      setInput(ctx.el, 'input[name="resetPasswordConfirm"]', 'Inne');
      clickByText(ctx.el, 'Ustaw hasło', 'button');
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Podane hasła różnią się od siebie.');

      setInput(ctx.el, 'input[name="resetPasswordConfirm"]', 'Tymczas123!');
      clickByText(ctx.el, 'Ustaw hasło', 'button');
      const req = ctx.http.expectOne(r => r.url === api('/api/users/u1/reset-password'));
      expect(req.request.body).toEqual({ newPassword: 'Tymczas123!' });
      req.flush(null);
      expect(toasts().map(t => t.message)).toContain('Hasło zresetowane – przy logowaniu użytkownik ustawi własne.');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Resetuj hasło', 'button');
      ctx.fixture.detectChanges();
      setInput(ctx.el, 'input[name="resetPasswordValue"]', 'a');
      setInput(ctx.el, 'input[name="resetPasswordConfirm"]', 'a');
      clickByText(ctx.el, 'Ustaw hasło', 'button');
      ctx.http.expectOne(r => r.url === api('/api/users/u1/reset-password')).flush({ title: 'Hasło musi mieć co najmniej 8 znaków.' }, { status: 400, statusText: 'Bad Request' });
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('co najmniej 8 znaków');
      clickByText(ctx.el, 'Anuluj', '.reset-row button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('input[name="resetPasswordValue"]')).toBeNull();
    });

    it('turns MFA off after confirmation', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
      const ctx = boot([{ ...linkedUser, mfaEnabled: true }]);
      openEdit(ctx);
      clickByText(ctx.el, 'Wyłącz MFA', 'button');
      ctx.http.expectNone(r => r.method === 'DELETE');

      clickByText(ctx.el, 'Wyłącz MFA', 'button');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === api('/api/users/u2/mfa')).flush({ ...linkedUser, mfaEnabled: false });
      ctx.fixture.detectChanges();

      expect(confirmSpy).toHaveBeenCalledTimes(2);
      expect(toasts().map(t => t.message)).toContain('Weryfikacja dwuetapowa wyłączona.');
      expect(ctx.el.querySelector('.modal')?.textContent).not.toContain('Wyłącz MFA');
      clickByText(ctx.el, 'Wyłącz konto', 'button');
    });

    it('shows refusals of MFA reset and of disabling', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot([{ ...linkedUser, mfaEnabled: true }]);
      openEdit(ctx);
      clickByText(ctx.el, 'Wyłącz MFA', 'button');
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Error' });
      clickByText(ctx.el, 'Wyłącz konto', 'button');
      ctx.http.expectOne(r => r.url === api('/api/users/u2/active')).flush({ title: 'Nie możesz wyłączyć własnego konta.' }, { status: 400, statusText: 'Bad Request' });
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).toContain('Nie możesz wyłączyć własnego konta.');
    });

    it('disables an account after confirmation and turns it back on', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
      const ctx = boot([linkedUser]);
      openEdit(ctx);
      clickByText(ctx.el, 'Wyłącz konto', 'button');
      ctx.http.expectNone(r => r.url.endsWith('/active'));

      clickByText(ctx.el, 'Wyłącz konto', 'button');
      const off = ctx.http.expectOne(r => r.url === api('/api/users/u2/active'));
      expect(off.request.body).toEqual({ isActive: false });
      off.flush({ ...linkedUser, isActive: false });
      ctx.fixture.detectChanges();
      expect(toasts().map(t => t.message)).toContain('Konto wyłączone.');

      clickByText(ctx.el, 'Włącz konto', 'button');
      const on = ctx.http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u2/active') && r.body.isActive === true);
      on.flush({ ...linkedUser, isActive: true });
      expect(toasts().map(t => t.message)).toContain('Konto włączone.');
      expect(confirmSpy).toHaveBeenCalledTimes(2);
    });
  });
  it('opens the edit window when the login is clicked', () => {
    const ctx = boot([linkedUser]);
    clickByText(ctx.el, 'anna@example.org', 'button.login-link');
    ctx.fixture.detectChanges();
    expect(ctx.fixture.componentInstance.editing()?.id).toBe('u2');
    expect(textOf(ctx.el)).toContain('Edytuj konto');
  });
});
