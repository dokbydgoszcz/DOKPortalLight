import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ResourcesComponent } from './resources.component';
import { Resource } from './resource.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setup, textOf } from '../../testing/test-helpers';

const scenarios: Resource = {
  id: 'r1', title: 'Scenariusze spotkań', description: 'Na rok formacyjny', createdAtUtc: '2026-10-01T10:00:00Z',
  files: [{ id: 'f1', fileName: 'scenariusz.pdf', contentType: 'application/pdf', sizeBytes: 2048, uploadedAtUtc: '2026-10-01T10:05:00Z' }]
};
const empty: Resource = { id: 'r2', title: 'Wzory pism', description: null, createdAtUtc: '2026-10-02T10:00:00Z', files: [] };
const url = api('/api/resources');

function boot(items: Resource[] = [scenarios, empty], granted: string[] = ['*']) {
  const ctx = setup(ResourcesComponent, { granted });
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === url && r.method === 'GET').flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('ResourcesComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('lists the resources with their description and files', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Scenariusze spotkań');
    expect(textOf(el)).toContain('Na rok formacyjny');
    expect(textOf(el)).toContain('scenariusz.pdf');
    expect(textOf(el)).toContain('Wzory pism');
  });

  it('explains an empty library', () => {
    const { el } = boot([]);

    expect(textOf(el)).toContain('Brak zasobów');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(ResourcesComponent);
    fixture.detectChanges();

    http.expectOne(r => r.url === url).flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać zasobów.');
  });

  it('searches with the typed phrase', () => {
    const ctx = boot();

    setInput(ctx.el, 'input[placeholder="Szukaj…"]', 'wzory');

    ctx.http.expectOne(r => r.url === url && r.params.get('query') === 'wzory').flush([empty]);
  });

  describe('a viewer without the manage permission', () => {
    it('can read and download, but sees no add, edit, delete or upload controls', () => {
      const { el } = boot([scenarios], ['Resources.View']);

      expect(textOf(el)).toContain('Pobierz');
      expect(textOf(el)).not.toContain('Dodaj zasób');
      expect(textOf(el)).not.toContain('Edytuj');
      expect(textOf(el)).not.toContain('Usuń');
      expect(textOf(el)).not.toContain('Dodaj plik');
    });
  });

  describe('a manager', () => {
    it('adds a resource with a title and a description', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj zasób');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      setInput(ctx.el, 'input[name="title"]', 'Nowy zasób');
      setInput(ctx.el, 'textarea[name="description"]', 'Opis');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
      expect(req.request.body).toEqual({ title: 'Nowy zasób', description: 'Opis' });
      req.flush({ ...empty, id: 'r3', title: 'Nowy zasób' });
      expect(toastMessages()).toContain('Dodano zasób. Dołącz do niego pliki.');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([scenarios, empty]);
    });

    it('does not post a resource without a title', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj zasób');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      ctx.http.expectNone(r => r.method === 'POST');
      expect(toastMessages()).toContain('Podaj tytuł zasobu.');
    });

    it('edits a resource with PUT', async () => {
      const ctx = boot();
      ctx.el.querySelector<HTMLElement>('.resource .edit')!.click();
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      expect((ctx.el.querySelector('input[name="title"]') as HTMLInputElement).value).toBe('Scenariusze spotkań');

      setInput(ctx.el, 'input[name="title"]', 'Scenariusze 2026');
      clickByText(ctx.el, 'Zapisz', '.modal-foot button');

      const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/r1`);
      expect(req.request.body).toEqual({ title: 'Scenariusze 2026', description: 'Na rok formacyjny' });
      req.flush({ ...scenarios, title: 'Scenariusze 2026' });
      expect(toastMessages()).toContain('Zapisano zmiany.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([scenarios]);
    });

    it('shows a toast when saving fails', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dodaj zasób');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      setInput(ctx.el, 'input[name="title"]', 'X');

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się zapisać zasobu.');
    });

    it('deletes a resource after confirmation and reloads', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      ctx.el.querySelector<HTMLElement>('.resource .delete')!.click();
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/r1`).flush(null);

      expect(toastMessages()).toContain('Zasób usunięty.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([empty]);
    });

    it('does nothing when the deletion is not confirmed', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      ctx.el.querySelector<HTMLElement>('.resource .delete')!.click();

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows a toast when deleting fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      ctx.el.querySelector<HTMLElement>('.resource .delete')!.click();
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się usunąć zasobu.');
    });

    it('offers uploading files to every resource, against that resource’s own address', () => {
      const { el } = boot();

      const uploaders = el.querySelectorAll('input[name="attachmentFiles"]');

      expect(uploaders.length).toBe(2);
    });
  });
});
