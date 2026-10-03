import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PeopleListComponent } from './people-list.component';
import { Person } from './person.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setup, textOf } from '../../testing/test-helpers';

const anna: Person = {
  id: '1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: 'anna@example.org', phone: '600100200',
  birthDate: null, parishId: null, parishName: null, notes: 'Uwaga', nameDayMonth: 7, nameDayDay: 26
};
const peopleUrl = api('/api/people');

function boot(items: Person[] = [anna], totalCount = items.length) {
  const ctx = setup(PeopleListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === peopleUrl).flush({ items, totalCount, page: 1, pageSize: 20 });
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('PeopleListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders people returned from the search endpoint', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Anna Maj');
    expect(textOf(el)).toContain('anna@example.org');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(PeopleListComponent);
    fixture.detectChanges();

    http.expectOne(r => r.url === peopleUrl).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy osób.');
  });

  it('searches from the first page with the typed query', () => {
    const ctx = boot([anna], 45);
    clickByText(ctx.el, 'Następna');
    ctx.http.expectOne(r => r.url === peopleUrl && r.params.get('page') === '2').flush({ items: [anna], totalCount: 45, page: 2, pageSize: 20 });
    ctx.fixture.detectChanges();

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'Maj');

    const req = ctx.http.expectOne(r => r.url === peopleUrl && r.params.get('query') === 'Maj');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [anna], totalCount: 1, page: 1, pageSize: 20 });
  });

  describe('adding a person', () => {
    it('posts the typed data, confirms and reloads', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj osobę');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      setInput(ctx.el, 'input[name="firstName"]', 'Ola');
      setInput(ctx.el, 'input[name="lastName"]', 'Nowak');
      setInput(ctx.el, 'input[name="email"]', 'ola@example.org');
      setInput(ctx.el, 'input[name="phone"]', '600');
      setInput(ctx.el, 'textarea[name="notes"]', 'Notatka');
      setInput(ctx.el, 'input[name="nameDayMonth"]', '6');
      setInput(ctx.el, 'input[name="nameDayDay"]', '15');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === peopleUrl);
      expect(req.request.body).toEqual({
        firstName: 'Ola', lastName: 'Nowak', email: 'ola@example.org', phone: '600', notes: 'Notatka', nameDayMonth: 6, nameDayDay: 15
      });
      req.flush({ ...anna, id: '2' });
      expect(toastMessages()).toContain('Dodano osobę.');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === peopleUrl).flush({ items: [anna], totalCount: 1, page: 1, pageSize: 20 });
    });

    it('shows a toast when saving fails and closes the form on cancel', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj osobę');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się zapisać osoby.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('editing a person', () => {
    it('opens the form filled with the person and sends the changes with PUT', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      expect((ctx.el.querySelector('input[name="firstName"]') as HTMLInputElement).value).toBe('Anna');
      expect((ctx.el.querySelector('input[name="nameDayMonth"]') as HTMLInputElement).value).toBe('7');

      setInput(ctx.el, 'input[name="lastName"]', 'Maj-Kowalska');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${peopleUrl}/1`);
      expect(req.request.body).toEqual({
        firstName: 'Anna', lastName: 'Maj-Kowalska', email: 'anna@example.org', phone: '600100200', notes: 'Uwaga', nameDayMonth: 7, nameDayDay: 26
      });
      req.flush(anna);
      expect(toastMessages()).toContain('Zapisano zmiany.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === peopleUrl).flush({ items: [anna], totalCount: 1, page: 1, pageSize: 20 });
    });

    it('treats missing optional fields as undefined when opening the form', () => {
      const ctx = boot([{ ...anna, email: null, phone: null, notes: null, nameDayMonth: null, nameDayDay: null }]);

      clickByText(ctx.el, 'Edytuj');

      expect(ctx.fixture.componentInstance.formValue).toEqual({
        firstName: 'Anna', lastName: 'Maj', email: undefined, phone: undefined, notes: undefined, nameDayMonth: undefined, nameDayDay: undefined
      });
    });
  });

  describe('deleting a person', () => {
    it('deletes after confirmation, informs the user and reloads', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${peopleUrl}/1`).flush(null);

      expect(toastMessages()).toContain('Osoba usunięta.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === peopleUrl).flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    });

    it('does nothing when the confirmation is cancelled', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows a toast when deleting fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się usunąć osoby.');
    });
  });

  describe('duplicate contact data', () => {
    const emailTaken = { status: 409, title: 'Ten adres e-mail ma już: Anna Maj. Adres e-mail musi być unikalny.', code: 'EmailTaken', duplicates: [] };
    const phoneDuplicate = { status: 409, title: 'Ten numer telefonu ma już: Anna Maj.', code: 'PhoneDuplicate', duplicates: [] };

    async function openAddForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj osobę');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      setInput(ctx.el, 'input[name="firstName"]', 'Ola');
      setInput(ctx.el, 'input[name="lastName"]', 'Nowak');
      setInput(ctx.el, 'input[name="phone"]', '600100200');
      return ctx;
    }

    it('shows the server message and keeps the form open when the e-mail is already taken', async () => {
      const confirmSpy = vi.spyOn(window, 'confirm');
      const ctx = await openAddForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST' && r.url === peopleUrl).flush(emailTaken, { status: 409, statusText: 'Conflict' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain(emailTaken.title);
      expect(confirmSpy).not.toHaveBeenCalled();
      expect(ctx.el.querySelector('.modal')).not.toBeNull();
      ctx.http.expectNone(r => r.method === 'POST');
    });

    it('asks for confirmation on a duplicated phone and saves again with the confirmation', async () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = await openAddForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST' && r.url === peopleUrl).flush(phoneDuplicate, { status: 409, statusText: 'Conflict' });

      expect(confirmSpy).toHaveBeenCalledWith(`${phoneDuplicate.title} Zapisać mimo to?`);
      const retry = ctx.http.expectOne(r => r.method === 'POST' && r.url === peopleUrl);
      expect(retry.request.body.confirmDuplicate).toBe(true);
      expect(retry.request.body.phone).toBe('600100200');
      retry.flush({ ...anna, id: '2' });
      expect(toastMessages()).toContain('Dodano osobę.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === peopleUrl).flush({ items: [anna], totalCount: 1, page: 1, pageSize: 20 });
    });

    it('does not save when the confirmation is declined and leaves the form open', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = await openAddForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST' && r.url === peopleUrl).flush(phoneDuplicate, { status: 409, statusText: 'Conflict' });
      ctx.fixture.detectChanges();

      ctx.http.expectNone(r => r.method === 'POST');
      expect(ctx.el.querySelector('.modal')).not.toBeNull();
      expect(toastMessages()).not.toContain('Nie udało się zapisać osoby.');
    });

    it('also confirms a duplicated phone when editing a person', async () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${peopleUrl}/1`).flush(phoneDuplicate, { status: 409, statusText: 'Conflict' });

      const retry = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${peopleUrl}/1`);
      expect(retry.request.body.confirmDuplicate).toBe(true);
      retry.flush(anna);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === peopleUrl).flush({ items: [anna], totalCount: 1, page: 1, pageSize: 20 });
    });

    it('keeps the generic message for other conflicts and errors', async () => {
      const ctx = await openAddForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST').flush({ status: 409, title: 'Inny konflikt' }, { status: 409, statusText: 'Conflict' });

      expect(toastMessages()).toContain('Nie udało się zapisać osoby.');
    });
  });
});
