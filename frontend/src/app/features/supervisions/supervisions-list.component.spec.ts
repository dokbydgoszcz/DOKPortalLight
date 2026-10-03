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

    it('shows a toast when adding fails and closes the form on cancel', async () => {
      const ctx = await openForm();

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
});
