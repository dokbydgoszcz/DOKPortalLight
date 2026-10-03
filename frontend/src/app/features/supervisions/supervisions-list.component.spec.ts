import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SupervisionsListComponent } from './supervisions-list.component';
import { Supervision } from './supervision.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const supervision: Supervision = {
  id: 's1', institution: 'DOK', groupLabel: 'Grupa A', supervisionDate: '2026-09-30',
  attendeesCount: 8, expectedCount: 10, topic: 'Modlitwa', conclusion: 'Spotkać się częściej'
};
const url = api('/api/supervisions');

function boot(items: Supervision[] = [supervision]) {
  const ctx = setup(SupervisionsListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === url).flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);
const saveButton = (el: HTMLElement) =>
  Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

describe('SupervisionsListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders supervisions returned from the API', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Grupa A');
    expect(textOf(el)).toContain('8/10');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(SupervisionsListComponent);
    fixture.detectChanges();

    http.expectOne(r => r.url === url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy superwizji.');
  });

  describe('adding a supervision', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Nowa superwizja');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('posts the typed supervision with the chosen institution', async () => {
      const ctx = await openForm();
      expect(textOf(ctx.el)).toContain('Nowa superwizja');

      setSelect(ctx.el, 'select[name="institution"]', 'SKSP');
      setInput(ctx.el, 'input[name="groupLabel"]', 'Grupa B');
      setInput(ctx.el, 'input[name="supervisionDate"]', '2026-10-10');
      setInput(ctx.el, 'textarea[name="topic"]', 'Liturgia');
      setInput(ctx.el, 'textarea[name="conclusion"]', 'Kontynuować');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({
        institution: 'SKSP', groupLabel: 'Grupa B', supervisionDate: '2026-10-10', topic: 'Liturgia', conclusion: 'Kontynuować'
      });
      req.flush(supervision);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano superwizję.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([supervision]);
    });

    it('keeps saving disabled until a date is chosen, so an empty date is never sent', async () => {
      const ctx = await openForm();
      expect(saveButton(ctx.el).disabled).toBe(true);
      expect(textOf(ctx.el)).toContain('Uzupełnij pola oznaczone *');
      expect(ctx.el.querySelector('.field .required')).not.toBeNull();

      saveButton(ctx.el).click();
      ctx.http.expectNone(r => r.method === 'POST');

      setInput(ctx.el, 'input[name="supervisionDate"]', '2026-10-10');
      ctx.fixture.detectChanges();
      expect(saveButton(ctx.el).disabled).toBe(false);
      expect(textOf(ctx.el)).not.toContain('Uzupełnij pola oznaczone *');
    });

    it('shows a toast when adding fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setInput(ctx.el, 'input[name="supervisionDate"]', '2026-10-10');
      ctx.fixture.detectChanges();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać superwizji.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('editing a supervision', () => {
    async function openEdit() {
      const ctx = boot();
      clickByText(ctx.el, 'Edytuj');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('opens the form filled with the supervision and sends the change with PUT', async () => {
      const ctx = await openEdit();
      expect(textOf(ctx.el)).toContain('Edytuj superwizję');
      expect((ctx.el.querySelector('input[name="groupLabel"]') as HTMLInputElement).value).toBe('Grupa A');
      expect((ctx.el.querySelector('select[name="institution"]') as HTMLSelectElement).value).toBe('DOK');

      setInput(ctx.el, 'textarea[name="topic"]', 'Pismo Święte');
      saveButton(ctx.el).click();

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/s1`);
      expect(req.request.body).toEqual({
        institution: 'DOK', groupLabel: 'Grupa A', supervisionDate: '2026-09-30', attendeesCount: 8, expectedCount: 10,
        topic: 'Pismo Święte', conclusion: 'Spotkać się częściej'
      });
      req.flush(supervision);
      expect(toastMessages()).toContain('Zapisano zmiany.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([supervision]);
    });

    it('shows a different toast when the update fails', async () => {
      const ctx = await openEdit();

      saveButton(ctx.el).click();
      ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się zapisać zmian.');
    });

    it('maps missing optional values to undefined when opening the form', () => {
      const ctx = boot([{ ...supervision, attendeesCount: null, expectedCount: null, topic: null, conclusion: null }]);

      clickByText(ctx.el, 'Edytuj');

      expect(ctx.fixture.componentInstance.newSupervision).toEqual({
        institution: 'DOK', groupLabel: 'Grupa A', supervisionDate: '2026-09-30',
        attendeesCount: undefined, expectedCount: undefined, topic: undefined, conclusion: undefined
      });
    });
  });

  describe('deleting a supervision', () => {
    it('deletes after confirmation and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć superwizję „Grupa A”?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/s1`).flush(null);

      expect(toastMessages()).toContain('Superwizja usunięta.');
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

      expect(toastMessages()).toContain('Nie udało się usunąć superwizji.');
    });
  });

  describe('filtering and sorting', () => {
    const groupA: Supervision = { ...supervision, id: 'a', institution: 'DOK', groupLabel: 'Grupa A', supervisionDate: '2026-09-30' };
    const groupB: Supervision = { ...supervision, id: 'b', institution: 'SKSP', groupLabel: 'Grupa B', supervisionDate: '2026-10-05' };
    const groupC: Supervision = { ...supervision, id: 'c', institution: 'DOK', groupLabel: 'Grupa C', supervisionDate: '2026-10-10' };
    const groupD: Supervision = { ...supervision, id: 'd', institution: 'SKSP', groupLabel: 'Grupa D', supervisionDate: '2026-08-01' };
    const all = [groupA, groupB, groupC, groupD];

    const titles = (el: HTMLElement) =>
      Array.from(el.querySelectorAll('.list-title')).map(t => t.textContent!.trim().split(' — ')[0]);

    it('shows the newest supervision first by default, whatever order the server returns', () => {
      const { el } = boot(all);

      expect(titles(el)).toEqual(['Grupa C', 'Grupa B', 'Grupa A', 'Grupa D']);
    });

    it('sorts by date oldest first', () => {
      const { fixture, el } = boot(all);

      setSelect(el, 'select[name="sortBy"]', 'dateAsc');
      fixture.detectChanges();

      expect(titles(el)).toEqual(['Grupa D', 'Grupa A', 'Grupa B', 'Grupa C']);
    });

    it('sorts by institution, then by date newest first', () => {
      const { fixture, el } = boot(all);

      setSelect(el, 'select[name="sortBy"]', 'institution');
      fixture.detectChanges();

      expect(titles(el)).toEqual(['Grupa C', 'Grupa A', 'Grupa B', 'Grupa D']);
    });

    it('asks the server for one institution when filtered, and for all again when cleared', () => {
      const { fixture, http, el } = boot(all);

      setSelect(el, 'select[name="institutionFilter"]', 'SKSP');
      const filtered = http.expectOne(r => r.url === url && r.params.get('institution') === 'SKSP');
      filtered.flush([groupB, groupD]);
      fixture.detectChanges();
      expect(titles(el)).toEqual(['Grupa B', 'Grupa D']);

      setSelect(el, 'select[name="institutionFilter"]', '');
      const cleared = http.expectOne(r => r.url === url && !r.params.has('institution'));
      cleared.flush(all);
      fixture.detectChanges();
      expect(titles(el)).toHaveLength(4);
    });

    it('keeps the chosen sorting when the filter changes', () => {
      const { fixture, http, el } = boot(all);
      setSelect(el, 'select[name="sortBy"]', 'dateAsc');
      fixture.detectChanges();

      setSelect(el, 'select[name="institutionFilter"]', 'DOK');
      http.expectOne(r => r.url === url && r.params.get('institution') === 'DOK').flush([groupC, groupA]);
      fixture.detectChanges();

      expect(titles(el)).toEqual(['Grupa A', 'Grupa C']);
    });

    it('shows the date in a readable format and the institution name', () => {
      const { el } = boot([groupA]);

      expect(textOf(el)).toContain('Grupa A — 30.09.2026');
      expect(el.querySelector('.list-sub')!.textContent).toContain('DOK');
    });
  });
});
