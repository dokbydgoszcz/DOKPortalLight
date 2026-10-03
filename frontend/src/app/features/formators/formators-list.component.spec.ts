import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FormatorsListComponent } from './formators-list.component';
import { Formator } from './formator.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const formator: Formator = {
  id: '1', personId: 'p1', personFullName: 'Joanna Lis', personEmail: 'j.lis@example.org', personPhone: null, function: 'Wykładowca'
};
const people = [
  { id: 'p1', firstName: 'Joanna', lastName: 'Lis', fullName: 'Joanna Lis', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null },
  { id: 'p2', firstName: 'Ewa', lastName: 'Nowak', fullName: 'Ewa Nowak', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const url = api('/api/formators');

function boot(items: Formator[] = [formator]) {
  const ctx = setup(FormatorsListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);
const saveButton = (el: HTMLElement) =>
  Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

describe('FormatorsListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders formators returned from the API', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Joanna Lis');
    expect(textOf(el)).toContain('Wykładowca');
  });

  it('shows toasts when the formators or the people cannot be loaded', () => {
    const { fixture, http } = setup(FormatorsListComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => r.url === api('/api/people')).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy formatorów.');
    expect(toastMessages()).toContain('Nie udało się wczytać listy osób.');
  });

  describe('adding a formator', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj formatora');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('requires a person and posts the chosen person with the function', async () => {
      const ctx = await openForm();
      expect(textOf(ctx.el)).toContain('Dodaj formatora');
      expect(saveButton(ctx.el).disabled).toBe(true);

      setSelect(ctx.el, 'select[name="personId"]', 'p2');
      setInput(ctx.el, 'input[name="function"]', 'Moderator');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);

      saveButton(ctx.el).click();
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ personId: 'p2', function: 'Moderator' });
      req.flush({ ...formator, id: '2' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano formatora.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([formator]);
    });

    it('shows a toast when adding fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setSelect(ctx.el, 'select[name="personId"]', 'p2');
      ctx.fixture.detectChanges();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać formatora.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('editing a formator', () => {
    async function openEdit() {
      const ctx = boot();
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the formator and sends the change with PUT', async () => {
      const ctx = await openEdit();
      expect(textOf(ctx.el)).toContain('Edytuj formatora');
      expect((ctx.el.querySelector('input[name="function"]') as HTMLInputElement).value).toBe('Wykładowca');
      expect((ctx.el.querySelector('select[name="personId"]') as HTMLSelectElement).value).toBe('p1');

      setInput(ctx.el, 'input[name="function"]', 'Referent');
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/1`);
      expect(req.request.body).toEqual({ personId: 'p1', function: 'Referent' });
      req.flush(formator);
      expect(toastMessages()).toContain('Zapisano zmiany.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([formator]);
    });

    it('shows a different toast when the update fails', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
    });
  });

  describe('deleting a formator', () => {
    it('deletes after confirmation and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć formatora „Joanna Lis”?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/1`).flush(null);

      expect(toastMessages()).toContain('Formator usunięty.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([]);
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

      expect(toastMessages()).toContain('Nie udało się usunąć formatora.');
    });
  });
});
