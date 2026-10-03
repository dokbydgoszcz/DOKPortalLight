import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DokCasesListComponent } from './dok-cases-list.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, click, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const dokCase = {
  id: '1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation',
  catechistPersonId: 'c1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null,
  lastMeetingDate: null, completedAtUtc: null
};
const secondCase = { ...dokCase, id: '2', personId: 'p2', personFullName: 'Piotr Malinowski', path: 'Conversion', stage: 'Sacrament', catechistPersonId: 'c2', catechistFullName: 'Maria Kaczmarek' };
const people = [
  { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null },
  { id: 'c1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const casesUrl = api('/api/dok-cases');

function boot(items = [dokCase, secondCase], totalCount = items.length) {
  const ctx = setup(DokCasesListComponent);
  ctx.fixture.detectChanges();
  const lists = ctx.http.match(r => r.url === casesUrl);
  expect(lists.length).toBe(2);
  lists.forEach(r => r.flush({ items, totalCount, page: 1, pageSize: 20 }));
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.fixture.detectChanges();
  return ctx;
}

function flushReload(ctx: ReturnType<typeof boot>, items = [dokCase, secondCase]) {
  ctx.http.match(r => r.url === casesUrl).forEach(r => r.flush(paged(items)));
  ctx.fixture.detectChanges();
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('DokCasesListComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('shows the per-path count computed from the fetched list', () => {
    const { el } = boot();

    const text = textOf(el);
    expect(text).toContain('Jan Kowalski');
    expect(text).toContain('Piotr Malinowski');
    expect(text).toContain('Bierzmowanie');
    expect(text).toContain('Konwersja');
  });

  it('shows a toast when the list cannot be loaded', () => {
    const { fixture, http } = setup(DokCasesListComponent);
    fixture.detectChanges();

    http.match(r => r.url === casesUrl)[0].flush('x', { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wczytać listy podopiecznych.');
  });

  it('translates known paths and falls back to the raw value for unknown ones', () => {
    const { fixture } = boot();

    expect(fixture.componentInstance.pathLabel('Communion')).toBe('Stół Pański');
    expect(fixture.componentInstance.pathLabel('Nowa')).toBe('Nowa');
  });

  it('loads the requested page when the pagination is used', () => {
    const ctx = boot([dokCase], 45);

    clickByText(ctx.el, 'Następna');

    ctx.http.expectOne(r => r.url === casesUrl && r.params.get('page') === '2' && r.params.get('pageSize') === '20')
      .flush({ items: [secondCase], totalCount: 45, page: 2, pageSize: 20 });
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Piotr Malinowski');
  });

  describe('adding a case', () => {
    async function openForm() {
      const ctx = boot();
      clickByText(ctx.el, 'Nowy podopieczny');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('keeps the save button disabled until a person and a catechist are chosen, then posts the form', async () => {
      const ctx = await openForm();
      const save = () => Array.from(ctx.el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;
      expect(save().disabled).toBe(true);

      setSelect(ctx.el, 'select[name="personId"]', 'p1');
      ctx.fixture.detectChanges();
      expect(save().disabled).toBe(true);
      setSelect(ctx.el, 'select[name="catechistPersonId"]', 'c1');
      setSelect(ctx.el, 'select[name="path"]', 'Confirmation');
      setSelect(ctx.el, 'select[name="stage"]', 'Formation');
      ctx.fixture.detectChanges();
      expect(save().disabled).toBe(false);

      save().click();
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === casesUrl);
      expect(req.request.body).toEqual({ personId: 'p1', path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c1' });
      req.flush(dokCase);
      ctx.fixture.detectChanges();

      expect(toastMessages()).toContain('Dodano podopiecznego.');
      expect(ctx.el.querySelector('.modal')).toBeNull();
      flushReload(ctx);
    });

    it('shows an error toast when saving fails and closes the form on cancel', async () => {
      const ctx = await openForm();
      setSelect(ctx.el, 'select[name="personId"]', 'p1');
      setSelect(ctx.el, 'select[name="catechistPersonId"]', 'c1');
      ctx.fixture.detectChanges();

      clickByText(ctx.el, 'Zapisz', '.modal-foot button');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
      expect(toastMessages()).toContain('Nie udało się dodać podopiecznego.');

      clickByText(ctx.el, 'Anuluj', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });
  });

  describe('deleting a case', () => {
    it('deletes after confirmation, informs the user and reloads', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${casesUrl}/1`).flush(null);

      expect(toastMessages()).toContain('Podopieczny usunięty.');
      flushReload(ctx, [secondCase]);
    });

    it('does nothing when the user cancels the confirmation', () => {
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

      expect(toastMessages()).toContain('Nie udało się usunąć podopiecznego.');
    });
  });

  describe('pastoral notes', () => {
    const notesUrl = api('/api/dok-cases/1/notes');
    const note = { id: 'n1', dokCaseId: '1', authorUserId: 'u1', authorEmail: 'kat@example.org', content: 'Pierwsza rozmowa', createdAtUtc: '2026-10-01T10:00:00Z' };

    async function openNotes(notes: unknown[] = [note]) {
      const ctx = boot();
      clickByText(ctx.el, 'Notatki');
      ctx.http.expectOne(notesUrl).flush(notes);
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('lists the notes of the chosen case and says when there are none', async () => {
      const ctx = await openNotes([note]);
      expect(textOf(ctx.el)).toContain('Pierwsza rozmowa');
      expect(textOf(ctx.el)).toContain('kat@example.org');

      clickByText(ctx.el, 'Zamknij', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();

      clickByText(ctx.el, 'Notatki');
      ctx.http.expectOne(notesUrl).flush([]);
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Brak notatek.');
    });

    it('does not post an empty note, but posts and reloads a real one', async () => {
      const ctx = await openNotes([]);

      clickByText(ctx.el, 'Dodaj notatkę');
      ctx.http.expectNone(r => r.method === 'POST');

      setInput(ctx.el, 'textarea', '  Spotkanie w czwartek  ');
      clickByText(ctx.el, 'Dodaj notatkę');
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === notesUrl);
      expect(req.request.body).toEqual({ content: 'Spotkanie w czwartek' });
      req.flush(note);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === notesUrl).flush([note]);

      expect(toastMessages()).toContain('Dodano notatkę.');
    });

    it('shows toasts when notes cannot be loaded or added', async () => {
      const ctx = boot();
      clickByText(ctx.el, 'Notatki');
      ctx.http.expectOne(notesUrl).flush('x', { status: 403, statusText: 'Forbidden' });
      expect(toastMessages()).toContain('Nie udało się wczytać notatek.');
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      setInput(ctx.el, 'textarea', 'Treść');
      clickByText(ctx.el, 'Dodaj notatkę');
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 403, statusText: 'Forbidden' });

      expect(toastMessages().some(m => m.startsWith('Nie udało się dodać notatki'))).toBe(true);
    });
  });

  describe('case documents', () => {
    const docsUrl = api('/api/dok-cases/1/documents');
    const missing = { id: 'd1', dokCaseId: '1', name: 'Metryka chrztu', isProvided: false, originalFileName: null, fileSizeBytes: null, uploadedAtUtc: null, hasFile: false };
    const provided = { ...missing, id: 'd2', name: 'Zaświadczenie', isProvided: true, originalFileName: 'zaswiadczenie.pdf', fileSizeBytes: 2048, uploadedAtUtc: '2026-10-01T10:00:00Z', hasFile: true };

    async function openDocuments(documents: unknown[] = [missing, provided]) {
      const ctx = boot();
      clickByText(ctx.el, 'Dokumenty');
      ctx.http.expectOne(docsUrl).flush(documents);
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      return ctx;
    }

    it('lists the documents with their file info', async () => {
      const ctx = await openDocuments();

      const text = textOf(ctx.el);
      expect(text).toContain('Metryka chrztu');
      expect(text).toContain('Brak przesłanego pliku.');
      expect(text).toContain('zaswiadczenie.pdf (2.0 KB)');
    });

    it('does not add an empty position, but posts a trimmed name and reloads', async () => {
      const ctx = await openDocuments([]);
      expect(textOf(ctx.el)).toContain('Brak pozycji na liście dokumentów.');

      clickByText(ctx.el, 'Dodaj pozycję');
      ctx.http.expectNone(r => r.method === 'POST');

      setInput(ctx.el, 'input[name="newDocumentName"]', '  Akt urodzenia ');
      clickByText(ctx.el, 'Dodaj pozycję');
      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === docsUrl);
      expect(req.request.body).toEqual({ name: 'Akt urodzenia' });
      req.flush(missing);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === docsUrl).flush([missing]);

      expect(toastMessages()).toContain('Dodano pozycję na liście dokumentów.');
    });

    it('uploads the chosen file as form data and reloads the list', async () => {
      const ctx = await openDocuments();
      const input = ctx.el.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(['abc'], 'metryka.pdf', { type: 'application/pdf' });
      Object.defineProperty(input, 'files', { value: [file] });

      input.dispatchEvent(new Event('change'));

      const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === `${docsUrl}/d1/upload`);
      expect(req.request.body instanceof FormData).toBe(true);
      expect((req.request.body as FormData).get('file')).toBe(file);
      req.flush(missing);
      ctx.http.expectOne(r => r.method === 'GET' && r.url === docsUrl).flush([missing]);
      expect(toastMessages()).toContain('Plik przesłany.');
    });

    it('ignores a file selection that has no file and reports upload errors', async () => {
      const ctx = await openDocuments();
      const input = ctx.el.querySelector('input[type="file"]') as HTMLInputElement;

      Object.defineProperty(input, 'files', { value: [], configurable: true });
      input.dispatchEvent(new Event('change'));
      ctx.http.expectNone(r => r.method === 'POST');

      Object.defineProperty(input, 'files', { value: [new File(['x'], 'a.pdf')], configurable: true });
      input.dispatchEvent(new Event('change'));
      ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 500, statusText: 'Server Error' });
      expect(toastMessages()).toContain('Nie udało się przesłać pliku.');
    });

    it('downloads a file under its original name', async () => {
      const ctx = await openDocuments();
      URL.createObjectURL = vi.fn(() => 'blob:test');
      URL.revokeObjectURL = vi.fn();
      const anchorClick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

      clickByText(ctx.el, 'Pobierz');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === `${docsUrl}/d2/download`).flush(new Blob(['x']));

      expect(anchorClick).toHaveBeenCalledTimes(1);
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    });

    it('shows a toast when the download fails and closes the modal on request', async () => {
      const ctx = await openDocuments();

      clickByText(ctx.el, 'Pobierz');
      ctx.http.expectOne(r => r.url === `${docsUrl}/d2/download`).error(new ProgressEvent('error'), { status: 500, statusText: 'Server Error' });
      expect(toastMessages()).toContain('Nie udało się pobrać pliku.');

      clickByText(ctx.el, 'Zamknij', '.modal-foot button');
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector('.modal')).toBeNull();
    });

    it('formats file sizes in B, KB and MB', async () => {
      const { fixture } = boot();
      const component = fixture.componentInstance;

      expect(component.formatFileSize(null)).toBe('');
      expect(component.formatFileSize(512)).toBe('512 B');
      expect(component.formatFileSize(2048)).toBe('2.0 KB');
      expect(component.formatFileSize(3 * 1024 * 1024)).toBe('3.0 MB');
    });

    it('reports a failure to load the documents', () => {
      const ctx = boot();
      clickByText(ctx.el, 'Dokumenty');

      ctx.http.expectOne(docsUrl).flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się wczytać listy dokumentów.');
    });
  });
});
