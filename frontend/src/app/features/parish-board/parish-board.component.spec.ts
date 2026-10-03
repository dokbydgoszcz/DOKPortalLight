import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ParishBoardComponent } from './parish-board.component';
import { ParishNeed } from './parish-need.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const openNeed: ParishNeed = {
  id: 'n1', parishId: 'pa1', parishName: 'św. Mateusza', description: 'Potrzebny katechista', status: 'Open',
  assignedPeople: []
};
const assignedNeed: ParishNeed = {
  id: 'n2', parishId: 'pa2', parishName: 'Matki Bożej', description: 'Grupa młodzieżowa', status: 'Assigned',
  assignedPeople: [
    { personId: 'p1', fullName: 'Anna Maj', assignedAtUtc: '2026-10-01T10:00:00Z' },
    { personId: 'p2', fullName: 'Marek Zielinski', assignedAtUtc: '2026-10-02T10:00:00Z' }
  ]
};
const parishes = [{ id: 'pa1', name: 'św. Mateusza', city: 'Bydgoszcz' }, { id: 'pa2', name: 'Matki Bożej', city: 'Toruń' }];
const people = [
  { id: 'p1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null },
  { id: 'p2', firstName: 'Marek', lastName: 'Zielinski', fullName: 'Marek Zielinski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null },
  { id: 'p3', firstName: 'Beata', lastName: 'Lis', fullName: 'Beata Lis', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const url = api('/api/parish-needs');

function boot(needs: ParishNeed[] = [openNeed, assignedNeed]) {
  const ctx = setup(ParishBoardComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(needs);
  ctx.http.expectOne(api('/api/parishes')).flush(parishes);
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('ParishBoardComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders the needs with their status and the assigned catechist', () => {
    const { el } = boot();

    const text = textOf(el);
    expect(text).toContain('św. Mateusza — Potrzebny katechista');
    expect(text).toContain('Skierowano:');
    expect(text).toContain('Anna Maj');
    expect(text).toContain('Marek Zielinski');
    expect(text).toContain('Assigned');
    expect(el.querySelectorAll('.list-row button.small').length).toBe(2);
  });

  it('shows empty-state text when there are no needs', () => {
    const { el } = boot([]);

    expect(textOf(el)).toContain('Brak zapotrzebowań.');
  });

  it('shows toasts when needs, parishes or people cannot be loaded', () => {
    const { fixture, http } = setup(ParishBoardComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne(api('/api/parishes')).flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => r.url === api('/api/people')).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toEqual(expect.arrayContaining([
      'Nie udało się wczytać zapotrzebowań parafii.',
      'Nie udało się wczytać listy parafii.',
      'Nie udało się wczytać listy osób.'
    ]));
  });

  describe('adding a need', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Nowe zapotrzebowanie');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('posts the chosen parish and description, confirms and reloads', async () => {
      const ctx = await openForm();

      setSelect(ctx.el, 'select[name="parishId"]', 'pa2');
      setInput(ctx.el, 'textarea[name="description"]', 'Potrzebny wikariusz');
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ parishId: 'pa2', description: 'Potrzebny wikariusz' });
      req.flush(openNeed);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano zapotrzebowanie.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([openNeed]);
    });

    it('keeps saving disabled, with a hint, until both the parish and the description are filled', async () => {
      const ctx = await openForm();
      const save = () => Array.from(ctx.el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

      expect(save().disabled).toBe(true);
      expect(textOf(ctx.el)).toContain('Uzupełnij pola oznaczone *');
      save().click();
      ctx.http.expectNone(r => r.method === 'POST');

      setSelect(ctx.el, 'select[name="parishId"]', 'pa2');
      setInput(ctx.el, 'textarea[name="description"]', 'Potrzebny wikariusz');
      ctx.fixture.detectChanges();
      expect(save().disabled).toBe(false);
    });

    it('closes the form on cancel', async () => {
      const ctx = await openForm();

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();

      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('assigning a catechist', () => {
    async function openAssign() {
      const ctx = boot();
      clickByText(ctx.el, 'Skieruj', '.list-row button');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('sends the chosen person with PUT and reloads', async () => {
      const ctx = await openAssign();
      expect(textOf(ctx.el)).toContain('Skieruj katechistę');

      setSelect(ctx.el, 'select[name="assignPersonId"]', 'p1');
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Skieruj', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/n1/assign`);
      expect(req.request.body).toEqual({ personId: 'p1' });
      req.flush(assignedNeed);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Skierowano katechistę.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([assignedNeed]);
    });

    it('keeps the button disabled until a person is chosen', async () => {
      const ctx = await openAssign();
      const confirm = () => ctx.el.querySelector('.modal-foot .btn.primary') as HTMLButtonElement;

      expect(confirm().disabled).toBe(true);
      setSelect(ctx.el, 'select[name="assignPersonId"]', 'p1');
      ctx.fixture.detectChanges();

      expect(confirm().disabled).toBe(false);
    });

    it('shows a toast when assigning fails and closes the dialog on cancel', async () => {
      const ctx = await openAssign();
      setSelect(ctx.el, 'select[name="assignPersonId"]', 'p1');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Skieruj', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      expect(toastMessages()).toContain('Nie udało się skierować katechisty.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });

    it('shows the server message when the person cannot be assigned', async () => {
      const ctx = await openAssign();
      setSelect(ctx.el, 'select[name="assignPersonId"]', 'p1');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Skieruj', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'PUT').flush({ title: 'Nie znaleziono osoby.' }, { status: 400, statusText: 'Bad Request' });

      expect(toastMessages()).toContain('Nie znaleziono osoby.');
    });

    it('offers only people who are not yet assigned to this need', () => {
      const ctx = boot();
      clickByText(ctx.el, '＋ Kolejna osoba', '.list-row button');
      ctx.fixture.detectChanges();

      const options = Array.from(ctx.el.querySelectorAll('select[name="assignPersonId"] option')).map(o => o.textContent!.trim());
      expect(options).toEqual(['— wybierz —', 'Beata Lis']);
    });

    it('does nothing when confirming without an open assignment', () => {
      const ctx = boot();

      ctx.fixture.componentInstance.confirmAssign();

      ctx.http.expectNone(r => r.method === 'PUT');
    });
  });

  describe('deleting a need', () => {
    it('deletes after confirmation and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć zapotrzebowanie „Potrzebny katechista”?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/n1`).flush(null);

      expect(toastMessages()).toContain('Zapotrzebowanie usunięte.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([assignedNeed]);
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

      expect(toastMessages()).toContain('Nie udało się usunąć zapotrzebowania.');
    });
  });

  describe('several catechists', () => {
    it('lets you add another person to a need that already has someone', async () => {
      const ctx = boot();
      clickByText(ctx.el, '＋ Kolejna osoba', '.list-row button');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      setSelect(ctx.el, 'select[name="assignPersonId"]', 'p3');
      ctx.fixture.detectChanges();
      clickByText(ctx.el, 'Skieruj', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/n2/assign`);
      expect(req.request.body).toEqual({ personId: 'p3' });
      req.flush(assignedNeed);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([assignedNeed]);
    });

    it('removes one person after confirmation and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      (ctx.el.querySelector('.assigned-person .link') as HTMLElement).click();
      expect(confirmSpy).toHaveBeenCalledWith('Odpiąć „Anna Maj” od zapotrzebowania?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/n2/assign/p1`).flush(assignedNeed);

      expect(toastMessages()).toContain('Odpięto osobę.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([assignedNeed]);
    });

    it('does nothing when the removal is declined, and reports a failure', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();
      const link = () => ctx.el.querySelector('.assigned-person .link') as HTMLElement;

      link().click();
      ctx.http.expectNone(r => r.method === 'DELETE');

      confirmSpy.mockReturnValue(true);
      link().click();
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });
      expect(toastMessages()).toContain('Nie udało się odpiąć osoby.');
    });

    it('hides adding and removing people from users who cannot manage needs', () => {
      const ctx = setup(ParishBoardComponent, { granted: ['ParishNeeds.View'] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush([assignedNeed]);
      ctx.http.expectOne(api('/api/parishes')).flush(parishes);
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
      ctx.fixture.detectChanges();

      expect(textOf(ctx.el)).toContain('Anna Maj');
      expect(ctx.el.querySelector('.assigned-person .link')).toBeNull();
      expect(ctx.el.querySelector('.list-row button')).toBeNull();
      expect(textOf(ctx.el)).not.toContain('Edytuj');
    });
  });

  describe('editing a need', () => {
    const saveButton = (el: HTMLElement) =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

    async function openEdit(rowText = 'Grupa młodzieżowa') {
      const ctx = boot();
      const row = Array.from(ctx.el.querySelectorAll('.list-row')).find(r => r.textContent!.includes(rowText)) as HTMLElement;
      (Array.from(row.querySelectorAll<HTMLElement>('.link')).find(l => l.textContent!.trim() === 'Edytuj'))!.click();
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the need, also after someone was assigned', async () => {
      const ctx = await openEdit();

      expect(textOf(ctx.el)).toContain('Edytuj zapotrzebowanie');
      expect((ctx.el.querySelector('select[name="parishId"]') as HTMLSelectElement).value).toBe('pa2');
      expect((ctx.el.querySelector('textarea[name="description"]') as HTMLTextAreaElement).value).toBe('Grupa młodzieżowa');
    });

    it('saves the change with PUT, confirms and reloads', async () => {
      const ctx = await openEdit();

      setSelect(ctx.el, 'select[name="parishId"]', 'pa1');
      setInput(ctx.el, 'textarea[name="description"]', 'Dwie grupy młodzieżowe');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/n2`);
      expect(req.request.body).toEqual({ parishId: 'pa1', description: 'Dwie grupy młodzieżowe' });
      req.flush(assignedNeed);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Zapisano zmiany.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([assignedNeed]);
    });

    it('shows the server message, or a generic one, and keeps the form open', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush({ title: 'Podaj opis zapotrzebowania.' }, { status: 400, statusText: 'Bad Request' });
      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Podaj opis zapotrzebowania.');
      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
      expect(ctx.el.querySelector('.modal')).not.toBeNull();
    });

    it('goes back to adding after an edit was cancelled', async () => {
      const ctx = await openEdit();
      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Nowe zapotrzebowanie');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      expect(textOf(ctx.el)).toContain('Nowe zapotrzebowanie parafii');
      expect((ctx.el.querySelector('textarea[name="description"]') as HTMLTextAreaElement).value).toBe('');
    });
  });
});
