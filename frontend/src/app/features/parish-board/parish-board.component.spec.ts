import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ParishBoardComponent } from './parish-board.component';
import { ParishNeed } from './parish-need.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const openNeed: ParishNeed = {
  id: 'n1', parishId: 'pa1', parishName: 'św. Mateusza', description: 'Potrzebny katechista', status: 'Open',
  assignedPersonId: null, assignedPersonName: null, assignedAtUtc: null
};
const assignedNeed: ParishNeed = {
  id: 'n2', parishId: 'pa2', parishName: 'Matki Bożej', description: 'Grupa młodzieżowa', status: 'Assigned',
  assignedPersonId: 'p1', assignedPersonName: 'Anna Maj', assignedAtUtc: '2026-10-01T10:00:00Z'
};
const parishes = [{ id: 'pa1', name: 'św. Mateusza', city: 'Bydgoszcz' }, { id: 'pa2', name: 'Matki Bożej', city: 'Toruń' }];
const people = [
  { id: 'p1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
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
    expect(text).toContain('Skierowano: Anna Maj');
    expect(text).toContain('Assigned');
    expect(el.querySelectorAll('.list-row button.small').length).toBe(1);
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
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ parishId: 'pa2', description: 'Potrzebny wikariusz' });
      req.flush(openNeed);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano zapotrzebowanie.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([openNeed]);
    });

    it('currently submits an empty parish when none was chosen and reports the backend error', async () => {
      const ctx = await openForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      const req = ctx.http.expectOne(r => r.method === 'POST');
      expect(req.request.body).toEqual({ parishId: '', description: '' });
      req.flush('x', { status: 400, statusText: 'Bad Request' });

      expect(toastMessages()).toContain('Nie udało się dodać zapotrzebowania.');
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
      clickByText(ctx.el, 'Skieruj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('sends the chosen person with PUT and reloads', async () => {
      const ctx = await openAssign();
      expect(textOf(ctx.el)).toContain('Skieruj katechistę');

      setSelect(ctx.el, 'select[name="assignPersonId"]', 'p1');
      clickByText(ctx.el, 'Skieruj', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/n1/assign`);
      expect(req.request.body).toEqual({ personId: 'p1' });
      req.flush(assignedNeed);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Skierowano katechistę.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([assignedNeed]);
    });

    it('shows a toast when assigning fails and closes the dialog on cancel', async () => {
      const ctx = await openAssign();

      clickByText(ctx.el, 'Skieruj', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się skierować katechisty.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
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
});
