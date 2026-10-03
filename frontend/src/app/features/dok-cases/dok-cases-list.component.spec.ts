import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DokCasesListComponent } from './dok-cases-list.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, click, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const dokCase = {
  id: '1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation',
  catechistPersonId: 'c1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null,
  lastMeetingDate: null, completedAtUtc: null, meetingsRecorded: 0, meetingsAttended: 0
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
    const note = { id: 'n1', dokCaseId: '1', authorUserId: 'u1', authorEmail: 'kat@example.org', content: 'Pierwsza rozmowa', createdAtUtc: '2026-10-01T10:00:00Z', attachments: [] };
    const attachment = { id: 'a1', fileName: 'kindle.pdf', contentType: 'application/pdf', sizeBytes: 4096, uploadedAtUtc: '2026-10-02T08:00:00Z' };

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

    describe('attachments', () => {
      const noteWithFile = { ...note, id: 'n2', content: 'Skan z Kindle', attachments: [attachment] };
      const pendingInput = (el: HTMLElement) => el.querySelector('input[name="newNoteFiles"]') as HTMLInputElement;

      function choose(input: HTMLInputElement, files: File[]) {
        Object.defineProperty(input, 'files', { value: files, configurable: true });
        input.dispatchEvent(new Event('change'));
      }

      it('shows the files attached to each note and downloads them from the note address', async () => {
        const ctx = await openNotes([note, noteWithFile]);
        URL.createObjectURL = vi.fn(() => 'blob:test');
        URL.revokeObjectURL = vi.fn();
        vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

        expect(textOf(ctx.el)).toContain('kindle.pdf');
        clickByText(ctx.el, 'Pobierz');

        ctx.http.expectOne(r => r.method === 'GET' && r.url === `${notesUrl}/n2/attachments/a1/download`).flush(new Blob(['x']));
        expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
      });

      it('adds a file to an existing note and reloads the notes', async () => {
        const ctx = await openNotes([note]);
        const input = ctx.el.querySelector('.list input[name="attachmentFiles"]') as HTMLInputElement;

        choose(input, [new File(['a'], 'kindle.pdf')]);
        ctx.http.expectOne(r => r.method === 'POST' && r.url === `${notesUrl}/n1/attachments`).flush(attachment);

        ctx.http.expectOne(r => r.method === 'GET' && r.url === notesUrl).flush([noteWithFile]);
        expect(toastMessages()).toContain('Dodano plik.');
      });

      it('lets users without write permission see and download files, but not add them', async () => {
        const ctx = setup(DokCasesListComponent, { granted: ['PastoralNotes.View'] });
        ctx.fixture.detectChanges();
        ctx.http.match(r => r.url === casesUrl).forEach(r => r.flush({ items: [dokCase], totalCount: 1, page: 1, pageSize: 20 }));
        ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
        ctx.fixture.detectChanges();
        clickByText(ctx.el, 'Notatki', '.link');
        ctx.http.expectOne(notesUrl).flush([noteWithFile]);
        ctx.fixture.detectChanges();

        expect(textOf(ctx.el)).toContain('kindle.pdf');
        expect(textOf(ctx.el)).toContain('Pobierz');
        expect(ctx.el.querySelector('input[type="file"]')).toBeNull();
      });

      it('attaches the files chosen for a new note right after the note is saved', async () => {
        const ctx = await openNotes([]);
        setInput(ctx.el, 'textarea', 'Skan z Kindle Scribe');

        choose(pendingInput(ctx.el), [new File(['a'], 'kindle.pdf'), new File(['b'], 'zdjecie.png')]);
        ctx.fixture.detectChanges();
        expect(textOf(ctx.el)).toContain('kindle.pdf');
        expect(textOf(ctx.el)).toContain('zdjecie.png');
        clickByText(ctx.el, 'Dodaj notatkę');

        const create = ctx.http.expectOne(r => r.method === 'POST' && r.url === notesUrl);
        expect(create.request.body).toEqual({ content: 'Skan z Kindle Scribe' });
        create.flush({ ...note, id: 'n9' });
        const uploads = ctx.http.match(r => r.method === 'POST' && r.url === `${notesUrl}/n9/attachments`);
        expect(uploads).toHaveLength(1);
        uploads[0].flush(attachment);
        ctx.http.expectOne(r => r.method === 'POST' && r.url === `${notesUrl}/n9/attachments`).flush(attachment);

        ctx.http.expectOne(r => r.method === 'GET' && r.url === notesUrl).flush([noteWithFile]);
        ctx.fixture.detectChanges();
        expect(toastMessages()).toContain('Dodano notatkę.');
        expect(textOf(ctx.el)).not.toContain('zdjecie.png');
      });

      it('lets a note consist only of files, describing it by the file names', async () => {
        const ctx = await openNotes([]);

        choose(pendingInput(ctx.el), [new File(['a'], 'kindle.pdf')]);
        ctx.fixture.detectChanges();
        clickByText(ctx.el, 'Dodaj notatkę');

        const create = ctx.http.expectOne(r => r.method === 'POST' && r.url === notesUrl);
        expect(create.request.body).toEqual({ content: 'Załączono: kindle.pdf' });
        create.flush({ ...note, id: 'n9' });
        ctx.http.expectOne(r => r.url === `${notesUrl}/n9/attachments`).flush(attachment);
        ctx.http.expectOne(r => r.method === 'GET' && r.url === notesUrl).flush([]);
      });

      it('refuses a forbidden file straight away and lets you drop a chosen one', async () => {
        const ctx = await openNotes([]);

        choose(pendingInput(ctx.el), [new File(['MZ'], 'virus.exe'), new File(['a'], 'kindle.pdf')]);
        ctx.fixture.detectChanges();

        expect(toastMessages().some(m => m.startsWith('virus.exe: Niedozwolony typ pliku'))).toBe(true);
        expect(textOf(ctx.el)).toContain('kindle.pdf');
        expect(textOf(ctx.el)).not.toContain('virus.exe');

        (ctx.el.querySelector('.pending-file .link') as HTMLElement).click();
        ctx.fixture.detectChanges();
        expect(textOf(ctx.el)).not.toContain('kindle.pdf');
        clickByText(ctx.el, 'Dodaj notatkę');
        ctx.http.expectNone(r => r.method === 'POST');
      });

      it('keeps the saved note and reports the files that failed to upload', async () => {
        const ctx = await openNotes([]);
        setInput(ctx.el, 'textarea', 'Notatka');
        choose(pendingInput(ctx.el), [new File(['a'], 'kindle.pdf')]);
        ctx.fixture.detectChanges();

        clickByText(ctx.el, 'Dodaj notatkę');
        ctx.http.expectOne(r => r.method === 'POST' && r.url === notesUrl).flush({ ...note, id: 'n9' });
        ctx.http.expectOne(r => r.url === `${notesUrl}/n9/attachments`).flush({ title: 'Plik jest za duży – maksymalny rozmiar to 20 MB.' }, { status: 400, statusText: 'Bad Request' });

        ctx.http.expectOne(r => r.method === 'GET' && r.url === notesUrl).flush([note]);
        expect(toastMessages()).toContain('kindle.pdf: Plik jest za duży – maksymalny rozmiar to 20 MB.');
        expect(toastMessages()).toContain('Dodano notatkę.');
      });
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
      const input = ctx.el.querySelector('input[type="file"]:not([name="bulkFiles"])') as HTMLInputElement;
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
      const input = ctx.el.querySelector('input[type="file"]:not([name="bulkFiles"])') as HTMLInputElement;

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

    describe('uploading files straight away', () => {
      const bulkInput = (el: HTMLElement) => el.querySelector('input[name="bulkFiles"]') as HTMLInputElement;

      function choose(input: HTMLInputElement, files: File[]) {
        Object.defineProperty(input, 'files', { value: files, configurable: true });
        input.dispatchEvent(new Event('change'));
      }

      it('offers the button even when the list is still empty, and explains it', async () => {
        const ctx = await openDocuments([]);

        expect(bulkInput(ctx.el)).not.toBeNull();
        expect(bulkInput(ctx.el).multiple).toBe(true);
        expect(bulkInput(ctx.el).accept).toBe('.pdf,.jpg,.jpeg,.png,.docx,.doc,.txt');
        expect(textOf(ctx.el)).toContain('Prześlij pliki');
        expect(textOf(ctx.el)).toContain('PDF, JPG/JPEG, PNG, DOCX, DOC, TXT');
      });

      it('creates a position named after each file and uploads the file to it', async () => {
        const ctx = await openDocuments([]);
        const metryka = new File(['a'], 'Metryka chrztu.pdf');
        const zdjecie = new File(['b'], 'Zdjęcie.PNG');

        choose(bulkInput(ctx.el), [metryka, zdjecie]);

        const first = ctx.http.expectOne(r => r.method === 'POST' && r.url === docsUrl);
        expect(first.request.body).toEqual({ name: 'Metryka chrztu' });
        first.flush({ ...missing, id: 'n1', name: 'Metryka chrztu' });
        const firstUpload = ctx.http.expectOne(r => r.method === 'POST' && r.url === `${docsUrl}/n1/upload`);
        expect((firstUpload.request.body as FormData).get('file')).toBe(metryka);
        firstUpload.flush(provided);

        const second = ctx.http.expectOne(r => r.method === 'POST' && r.url === docsUrl);
        expect(second.request.body).toEqual({ name: 'Zdjęcie' });
        second.flush({ ...missing, id: 'n2', name: 'Zdjęcie' });
        ctx.http.expectOne(r => r.method === 'POST' && r.url === `${docsUrl}/n2/upload`).flush(provided);

        ctx.http.expectOne(r => r.method === 'GET' && r.url === docsUrl).flush([provided]);
        expect(toastMessages()).toContain('Przesłano dokumenty: 2.');
      });

      it('confirms a single file in the singular', async () => {
        const ctx = await openDocuments([]);

        choose(bulkInput(ctx.el), [new File(['a'], 'metryka.pdf')]);
        ctx.http.expectOne(r => r.method === 'POST' && r.url === docsUrl).flush({ ...missing, id: 'n1' });
        ctx.http.expectOne(r => r.url === `${docsUrl}/n1/upload`).flush(provided);
        ctx.http.expectOne(r => r.method === 'GET' && r.url === docsUrl).flush([provided]);

        expect(toastMessages()).toContain('Przesłano dokument.');
      });

      it('refuses a forbidden file without creating any position', async () => {
        const ctx = await openDocuments([]);

        choose(bulkInput(ctx.el), [new File(['x'], 'arkusz.xlsx')]);

        ctx.http.expectNone(r => r.method === 'POST');
        expect(toastMessages().some(m => m.startsWith('arkusz.xlsx: Niedozwolony typ pliku'))).toBe(true);
      });

      it('does nothing when no file was chosen', async () => {
        const ctx = await openDocuments([]);

        choose(bulkInput(ctx.el), []);

        ctx.http.expectNone(r => r.method === 'POST');
      });

      it('reports a failed upload (the position stays) and a failed creation, and still reloads', async () => {
        const ctx = await openDocuments([]);

        choose(bulkInput(ctx.el), [new File(['a'], 'a.pdf'), new File(['b'], 'b.pdf')]);
        ctx.http.expectOne(r => r.method === 'POST' && r.url === docsUrl).flush({ ...missing, id: 'n1' });
        ctx.http.expectOne(r => r.url === `${docsUrl}/n1/upload`).flush({ title: 'Plik jest za duży – maksymalny rozmiar to 20 MB.' }, { status: 400, statusText: 'Bad Request' });
        ctx.http.expectOne(r => r.method === 'POST' && r.url === docsUrl).flush('x', { status: 500, statusText: 'Server Error' });

        ctx.http.expectOne(r => r.method === 'GET' && r.url === docsUrl).flush([missing]);
        expect(toastMessages()).toContain('a.pdf: Plik jest za duży – maksymalny rozmiar to 20 MB.');
        expect(toastMessages()).toContain('b.pdf: Nie udało się przesłać pliku.');
      });

      it('is hidden from users who cannot manage the documents', () => {
        const ctx = setup(DokCasesListComponent, { granted: ['CaseDocuments.View'] });
        ctx.fixture.detectChanges();
        ctx.http.match(r => r.url === casesUrl).forEach(r => r.flush({ items: [dokCase], totalCount: 1, page: 1, pageSize: 20 }));
        ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
        ctx.fixture.detectChanges();
        clickByText(ctx.el, 'Dokumenty', '.link');
        ctx.http.expectOne(docsUrl).flush([provided]);
        ctx.fixture.detectChanges();

        expect(ctx.el.querySelector('input[type="file"]')).toBeNull();
        expect(textOf(ctx.el)).not.toContain('Prześlij pliki');
      });
    });

    describe('replacing the file of a position', () => {
      it('checks the file against the rules before sending and shows the server message on failure', async () => {
        const ctx = await openDocuments();
        const input = ctx.el.querySelector('input[type="file"]:not([name="bulkFiles"])') as HTMLInputElement;

        Object.defineProperty(input, 'files', { value: [new File(['x'], 'arkusz.xlsx')], configurable: true });
        input.dispatchEvent(new Event('change'));
        ctx.http.expectNone(r => r.method === 'POST');
        expect(toastMessages().some(m => m.startsWith('Niedozwolony typ pliku'))).toBe(true);

        Object.defineProperty(input, 'files', { value: [new File(['x'], 'a.pdf')], configurable: true });
        input.dispatchEvent(new Event('change'));
        ctx.http.expectOne(r => r.method === 'POST').flush({ title: 'Plik jest za duży – maksymalny rozmiar to 20 MB.' }, { status: 400, statusText: 'Bad Request' });
        expect(toastMessages()).toContain('Plik jest za duży – maksymalny rozmiar to 20 MB.');
      });
    });
  });

  describe('attendance summary', () => {
    it('shows attended/recorded with a percentage, and a dash when nothing is recorded', () => {
      const { el } = boot([
        { ...dokCase, meetingsRecorded: 5, meetingsAttended: 4 },
        { ...secondCase, meetingsRecorded: 0, meetingsAttended: 0 }
      ]);

      const rows = Array.from(el.querySelectorAll('tbody tr'));
      expect(rows[0].textContent).toContain('4/5 (80%)');
      expect(rows[1].textContent).not.toContain('%');
      expect(rows[1].querySelector('.attendance')!.textContent!.trim()).toBe('—');
    });

    it('rounds the percentage to a whole number', () => {
      const { el } = boot([{ ...dokCase, meetingsRecorded: 3, meetingsAttended: 2 }]);

      expect(textOf(el)).toContain('2/3 (67%)');
    });
  });
});
