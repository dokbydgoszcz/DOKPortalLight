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

function fillAccount(el: HTMLElement, email: string, password: string, confirm: string) {
  setInput(el, 'input[name="email"]', email);
  setInput(el, 'input[name="password"]', password);
  setInput(el, 'input[name="confirmPassword"]', confirm);
}

function toasts() {
  return TestBed.inject(ToastService).toasts();
}

describe('UsersListComponent', () => {
  afterEach(() => vi.useRealTimers());

  it('renders emails returned from the API', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('admin@dokportal.local');
  });

  it('renders one role column per role returned by the API', () => {
    const { el } = boot();

    const headers = Array.from(el.querySelectorAll('thead th')).map(th => th.textContent!.trim());
    expect(headers).toContain('Biskup');
    expect(el.querySelectorAll('tbody input[type="checkbox"]').length).toBe(2);
  });

  it('shows toasts when the user list or the role list cannot be loaded', () => {
    const { fixture, http } = setup(UsersListComponent);
    fixture.detectChanges();

    http.expectOne(api('/api/users')).flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne(api('/api/users/roles')).flush('x', { status: 500, statusText: 'Server Error' });

    const messages = toasts().map(t => t.message);
    expect(messages).toContain('Nie udało się wczytać listy użytkowników.');
    expect(messages).toContain('Nie udało się wczytać listy ról.');
  });

  describe('creating an account', () => {
    it('rejects mismatched passwords without calling the API', () => {
      const { fixture, http, el } = boot();
      fillAccount(el, 'nowy@example.org', 'Sekret123!', 'inne');

      clickByText(el, 'Utwórz konto');
      fixture.detectChanges();

      expect(textOf(el)).toContain('Podane hasła różnią się od siebie.');
      http.expectNone(r => r.method === 'POST');
    });

    it('creates an account for a person found through the debounced search', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = boot();

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
      expect(el.querySelector('input[name="personQuery"]')).not.toBeNull();
    });

    it('debounces the search: only the last query is sent and a blank query sends nothing', () => {
      vi.useFakeTimers();
      const { http, el } = boot();

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
      const { http, el } = boot();

      setInput(el, 'input[name="personQuery"]', 'Jan');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people')).flush('x', { status: 500, statusText: 'Server Error' });

      expect(toasts().map(t => t.message)).toContain('Nie udało się wyszukać osób.');
    });

    it('lets the user change the selected person', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = boot();
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
      const { fixture, http, el } = boot();
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
      expect(personReq.request.body).toEqual({ firstName: 'Ewa', lastName: 'Nowak' });
      personReq.flush({ ...person, id: 'new-1', firstName: 'Ewa', lastName: 'Nowak', fullName: 'Ewa Nowak' });

      const userReq = http.expectOne(r => r.method === 'POST' && r.url === api('/api/users'));
      expect(userReq.request.body.personId).toBe('new-1');
    });

    it('shows an error when the new person cannot be created, and allows cancelling', () => {
      const { fixture, http, el } = boot();
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
      const { fixture, http, el } = boot();
      fillAccount(el, 'nowy@example.org', 'Sekret123!', 'Sekret123!');

      clickByText(el, 'Utwórz konto');
      http.expectOne(r => r.method === 'POST' && r.url === api('/api/users'))
        .flush('x', { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();

      expect(textOf(el)).toContain('Nie udało się utworzyć konta użytkownika.');
    });
  });

  describe('assigning roles', () => {
    it('sends the full role list when a role is added or removed and reloads afterwards', () => {
      const { fixture, http, el } = boot();
      const boxes = () => Array.from(el.querySelectorAll<HTMLInputElement>('tbody input[type="checkbox"]'));

      boxes()[1].checked = true;
      boxes()[1].dispatchEvent(new Event('change'));
      const add = http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u1/roles'));
      expect(add.request.body).toEqual({ roles: ['Administrator', 'Biskup'] });
      add.flush({ ...user, roles: ['Administrator', 'Biskup'] });
      flushAll(http, api('/api/users'), [{ ...user, roles: ['Administrator', 'Biskup'] }]);
      flushAll(http, api('/api/users/roles'), ['Administrator', 'Biskup']);
      fixture.detectChanges();

      boxes()[0].checked = false;
      boxes()[0].dispatchEvent(new Event('change'));
      const remove = http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u1/roles'));
      expect(remove.request.body).toEqual({ roles: ['Biskup'] });
    });

    it('shows a toast when the roles cannot be updated', () => {
      const { http, el } = boot();
      const box = el.querySelectorAll<HTMLInputElement>('tbody input[type="checkbox"]')[1];

      box.checked = true;
      box.dispatchEvent(new Event('change'));
      http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toasts().map(t => t.message)).toContain('Nie udało się zaktualizować ról.');
    });
  });

  describe('resetting a password', () => {
    function startReset() {
      const ctx = boot();
      clickByText(ctx.el, 'Resetuj hasło');
      ctx.fixture.detectChanges();
      return ctx;
    }

    it('rejects an empty or mismatched password', () => {
      const { fixture, http, el } = startReset();

      setInput(el, 'input[placeholder="Nowe hasło"]', 'Nowe12345!');
      setInput(el, 'input[placeholder="Powtórz"]', 'Inne12345!');
      clickByText(el, 'Zapisz');
      fixture.detectChanges();

      expect(textOf(el)).toContain('Podane hasła różnią się od siebie.');
      http.expectNone(r => r.method === 'PUT');
    });

    it('sends the new password, closes the editor and confirms with a toast', () => {
      const { fixture, http, el } = startReset();
      setInput(el, 'input[placeholder="Nowe hasło"]', 'Nowe12345!');
      setInput(el, 'input[placeholder="Powtórz"]', 'Nowe12345!');

      clickByText(el, 'Zapisz');
      const req = http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u1/reset-password'));
      expect(req.request.body).toEqual({ newPassword: 'Nowe12345!' });
      req.flush(null);
      fixture.detectChanges();

      expect(toasts().map(t => t.message)).toContain('Hasło zresetowane.');
      expect(el.querySelector('input[placeholder="Nowe hasło"]')).toBeNull();
    });

    it('shows an error when the reset fails and lets the user cancel', () => {
      const { fixture, http, el } = startReset();
      setInput(el, 'input[placeholder="Nowe hasło"]', 'Nowe12345!');
      setInput(el, 'input[placeholder="Powtórz"]', 'Nowe12345!');

      clickByText(el, 'Zapisz');
      http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();
      expect(textOf(el)).toContain('Nie udało się zresetować hasła.');

      clickByText(el, 'Anuluj');
      fixture.detectChanges();
      expect(el.querySelector('input[placeholder="Nowe hasło"]')).toBeNull();
    });
  });

  describe('linking accounts to people', () => {
    const rowOf = (el: HTMLElement, email: string) =>
      Array.from(el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(email)) as HTMLElement;
    const linkLink = (el: HTMLElement, email: string, text: string) =>
      Array.from(rowOf(el, email).querySelectorAll<HTMLElement>('.link, button')).find(e => e.textContent!.trim() === text)!;

    it('shows the linked person, or a note when the account has none', () => {
      const { el } = boot([linkedUser, unlinkedCatechist]);

      expect(el.querySelector('thead')!.textContent).toContain('Osoba');
      expect(rowOf(el, 'anna@example.org').textContent).toContain('Anna Maj');
      expect(rowOf(el, 'kat@example.org').textContent).toContain('brak');
      expect(rowOf(el, 'kat@example.org').textContent).toContain('nie zobaczy podopiecznych');
      expect(rowOf(el, 'anna@example.org').textContent).not.toContain('nie zobaczy');
    });

    it('searches for a person and links the account to the chosen one', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = boot([unlinkedCatechist]);

      linkLink(el, 'kat@example.org', 'Powiąż').click();
      fixture.detectChanges();
      setInput(el, 'input[name="linkQuery"]', 'Kowal');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people') && r.params.get('query') === 'Kowal').flush(paged([person]));
      fixture.detectChanges();
      clickByText(el, 'Jan Kowalski', '.link-results button');

      const req = http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u3/person'));
      expect(req.request.body).toEqual({ personId: 'p1' });
      req.flush({ ...unlinkedCatechist, personId: 'p1', personFullName: 'Jan Kowalski' });
      expect(toasts().map(t => t.message)).toContain('Konto powiązane z osobą.');
      flushAll(http, api('/api/users'), [{ ...unlinkedCatechist, personId: 'p1', personFullName: 'Jan Kowalski' }]);
      flushAll(http, api('/api/users/roles'), ['KatechistaProwadzacy']);
    });

    it('shows the server message when the person already has another account', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = boot([unlinkedCatechist]);
      linkLink(el, 'kat@example.org', 'Powiąż').click();
      fixture.detectChanges();
      setInput(el, 'input[name="linkQuery"]', 'Kowal');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people')).flush(paged([person]));
      fixture.detectChanges();
      clickByText(el, 'Jan Kowalski', '.link-results button');

      http.expectOne(r => r.method === 'PUT').flush({ title: 'Ta osoba ma już konto: jan@example.org.' }, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();

      expect(textOf(el)).toContain('Ta osoba ma już konto: jan@example.org.');
    });

    it('falls back to a generic message when the server gives no details', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = boot([unlinkedCatechist]);
      linkLink(el, 'kat@example.org', 'Powiąż').click();
      fixture.detectChanges();
      setInput(el, 'input[name="linkQuery"]', 'Kowal');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people')).flush(paged([person]));
      fixture.detectChanges();
      clickByText(el, 'Jan Kowalski', '.link-results button');

      http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(textOf(el)).toContain('Nie udało się powiązać konta z osobą.');
    });

    it('offers to change the person of a linked account and can cancel', () => {
      const { fixture, http, el } = boot([linkedUser]);

      linkLink(el, 'anna@example.org', 'Zmień').click();
      fixture.detectChanges();
      expect(el.querySelector('input[name="linkQuery"]')).not.toBeNull();

      clickByText(el, 'Anuluj', 'tbody button');
      fixture.detectChanges();

      expect(el.querySelector('input[name="linkQuery"]')).toBeNull();
      http.expectNone(r => r.method === 'PUT');
    });

    it('unlinks the account after confirmation', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const { http, el } = boot([linkedUser]);

      linkLink(el, 'anna@example.org', 'Odepnij').click();

      const req = http.expectOne(r => r.method === 'PUT' && r.url === api('/api/users/u2/person'));
      expect(req.request.body).toEqual({ personId: null });
      req.flush({ ...linkedUser, personId: null, personFullName: null });
      expect(toasts().map(t => t.message)).toContain('Konto odpięte od osoby.');
      flushAll(http, api('/api/users'), [linkedUser]);
      flushAll(http, api('/api/users/roles'), ['KatechistaProwadzacy']);
    });

    it('does nothing when unlinking is not confirmed', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const { http, el } = boot([linkedUser]);

      linkLink(el, 'anna@example.org', 'Odepnij').click();

      http.expectNone(r => r.method === 'PUT');
    });

    it('shows an error toast when unlinking fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const { http, el } = boot([linkedUser]);

      linkLink(el, 'anna@example.org', 'Odepnij').click();
      http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toasts().map(t => t.message)).toContain('Nie udało się odpiąć konta od osoby.');
    });

    it('clears the results when the search box is emptied', () => {
      vi.useFakeTimers();
      const { fixture, el, http } = boot([unlinkedCatechist]);
      linkLink(el, 'kat@example.org', 'Powiąż').click();
      fixture.detectChanges();
      setInput(el, 'input[name="linkQuery"]', 'Kowal');
      vi.advanceTimersByTime(300);
      http.expectOne(r => r.url === api('/api/people')).flush(paged([person]));
      fixture.detectChanges();
      expect(el.querySelectorAll('.link-results button').length).toBe(1);

      setInput(el, 'input[name="linkQuery"]', '   ');
      fixture.detectChanges();

      expect(el.querySelectorAll('.link-results button').length).toBe(0);
    });
  });
});
