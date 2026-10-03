import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ParishesListComponent } from './parishes-list.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setup, textOf } from '../../testing/test-helpers';

const parishes = [
  { id: 'pa1', name: 'św. Mateusza', city: 'Bydgoszcz' },
  { id: 'pa2', name: 'Matki Bożej', city: 'Toruń' }
];
const url = api('/api/parishes');

function boot(items = parishes) {
  const ctx = setup(ParishesListComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('ParishesListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders the parishes and an empty-state text', () => {
    const ctx = boot();
    expect(textOf(ctx.el)).toContain('św. Mateusza');
    expect(textOf(ctx.el)).toContain('Toruń');

    TestBed.resetTestingModule();
    expect(textOf(boot([]).el)).toContain('Brak parafii.');
  });

  it('filters the list by name or city while typing', () => {
    const ctx = boot();

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'toruń');
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Matki Bożej');
    expect(textOf(ctx.el)).not.toContain('św. Mateusza');

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'mateusza');
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('św. Mateusza');
    expect(textOf(ctx.el)).not.toContain('Matki Bożej');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(ParishesListComponent);
    fixture.detectChanges();

    http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy parafii.');
  });

  describe('adding a parish', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj parafię');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('refuses an empty name without calling the API', async () => {
      const ctx = await openForm();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      expect(toastMessages()).toContain('Podaj nazwę parafii.');
      ctx.http.expectNone(r => r.method === 'POST');
    });

    it('posts the typed parish, confirms and reloads', async () => {
      const ctx = await openForm();

      setInput(ctx.el, 'input[name="name"]', 'św. Antoniego');
      setInput(ctx.el, 'input[name="city"]', 'Bydgoszcz');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ name: 'św. Antoniego', city: 'Bydgoszcz' });
      req.flush(parishes[0]);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano parafię.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush(parishes);
    });

    it('shows a toast when adding fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setInput(ctx.el, 'input[name="name"]', 'św. Antoniego');

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać parafii.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('deleting a parish', () => {
    it('names the parish and city in the confirmation, deletes and reloads', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      expect(confirmSpy).toHaveBeenCalledWith('Usunąć parafię „św. Mateusza” (Bydgoszcz)?');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/pa1`).flush(null);

      expect(toastMessages()).toContain('Parafia usunięta.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([parishes[1]]);
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

      expect(toastMessages()).toContain('Nie udało się usunąć parafii.');
    });
  });
});
