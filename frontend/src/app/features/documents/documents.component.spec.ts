import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DocumentsComponent } from './documents.component';
import { GeneratedDocument } from './generated-document.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const history: GeneratedDocument[] = [{
  id: 'd1', template: 'LetterToBishop', personId: 'p1', personFullName: 'Jan Kowalski',
  generatedByUserId: 'u1', additionalNotes: null, createdAtUtc: '2026-09-22T10:00:00Z', hasStoredFile: true, downloadCount: 3
}];
const oldDocument: GeneratedDocument = { ...history[0], id: 'd2', template: 'DokReferral', hasStoredFile: false, downloadCount: 1 };
const people = [
  { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];
const url = api('/api/documents');

function boot(items: GeneratedDocument[] = history) {
  const ctx = setup(DocumentsComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.fixture.detectChanges();
  return ctx;
}

const generateButton = (el: HTMLElement) => el.querySelector('.card-body > button.primary') as HTMLButtonElement;
const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('DocumentsComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('renders the history of generated documents with readable template names', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('Pismo do Biskupa');
    expect(textOf(el)).toContain('Jan Kowalski');
  });

  it('says when nothing has been generated yet', () => {
    const { el } = boot([]);

    expect(textOf(el)).toContain('Brak wygenerowanych dokumentów.');
  });

  it('offers every template and keeps generating disabled until a person is chosen', () => {
    const { fixture, el } = boot();

    expect(el.querySelectorAll('select[name="template"] option').length).toBe(6);
    expect(generateButton(el).disabled).toBe(true);

    setSelect(el, 'select[name="personId"]', 'p1');
    fixture.detectChanges();
    expect(generateButton(el).disabled).toBe(false);
  });

  it('requests the chosen template for the chosen person and downloads the PDF, then reloads the history', async () => {
    const ctx = boot();
    await ctx.fixture.whenStable();
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();
    let downloadName = '';
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      downloadName = this.download;
    });

    setSelect(ctx.el, 'select[name="template"]', 'ConversionConsent');
    setSelect(ctx.el, 'select[name="personId"]', 'p1');
    setInput(ctx.el, 'textarea[name="additionalNotes"]', 'Pilne');
    ctx.fixture.detectChanges();
    generateButton(ctx.el).click();

    const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === `${url}/generate`);
    expect(req.request.body).toEqual({ template: 'ConversionConsent', personId: 'p1', additionalNotes: 'Pilne' });
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['%PDF']));

    expect(downloadName).toBe('ConversionConsent.pdf');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush(history);
  });

  it('tells the user when generating the document fails', async () => {
    const ctx = boot();
    await ctx.fixture.whenStable();
    setSelect(ctx.el, 'select[name="personId"]', 'p1');
    ctx.fixture.detectChanges();

    generateButton(ctx.el).click();
    ctx.http.expectOne(r => r.method === 'POST').error(new ProgressEvent('error'), { status: 500, statusText: 'Server Error' });

    expect(toastMessages()).toContain('Nie udało się wygenerować dokumentu.');
  });

  it('shows no generator card without the Documents.Generate permission', () => {
    const ctx = setup(DocumentsComponent, { granted: ['Documents.View'] });
    ctx.fixture.detectChanges();
    ctx.http.expectOne(url).flush(history);
    ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
    ctx.fixture.detectChanges();

    expect(clickByTextSafe(ctx.el, 'Generuj PDF')).toBe(false);
  });

  describe('history actions', () => {
    const rowOf = (el: HTMLElement, text: string) =>
      Array.from(el.querySelectorAll('tbody tr')).find(r => r.textContent!.includes(text)) as HTMLElement;
    const action = (el: HTMLElement, rowText: string, label: string) =>
      Array.from(rowOf(el, rowText).querySelectorAll<HTMLElement>('.link, button')).find(e => e.textContent!.trim() === label)!;

    function stubDownload() {
      URL.createObjectURL = vi.fn(() => 'blob:test');
      URL.revokeObjectURL = vi.fn();
      const names: string[] = [];
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
        names.push(this.download);
      });
      return names;
    }

    it('shows a readable date and the number of downloads', () => {
      const { el } = boot();

      const text = rowOf(el, 'Jan Kowalski').textContent!;
      expect(text).toMatch(/22\.09\.2026 \d{2}:\d{2}/);
      expect(text).not.toContain('2026-09-22T');
      expect(el.querySelector('thead')!.textContent).toContain('Pobrania');
      expect(rowOf(el, 'Jan Kowalski').querySelector('.download-count')!.textContent!.trim()).toBe('3');
    });

    it('marks entries without a stored file as restored from current data', () => {
      const { el } = boot([history[0], oldDocument]);

      expect(rowOf(el, 'Skierowanie do DOK').textContent).toContain('odtwarzane z aktualnych danych');
      expect(rowOf(el, 'Pismo do Biskupa').textContent).not.toContain('odtwarzane');
    });

    it('downloads the document again as a PDF and refreshes the download count', () => {
      const names = stubDownload();
      const ctx = boot();

      action(ctx.el, 'Jan Kowalski', 'Pobierz').click();

      const req = ctx.http.expectOne(r => r.method === 'GET' && r.url === `${url}/d1/download`);
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['%PDF']));
      expect(names).toEqual(['LetterToBishop.pdf']);
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([{ ...history[0], downloadCount: 4 }]);
      ctx.fixture.detectChanges();
      expect(rowOf(ctx.el, 'Jan Kowalski').querySelector('.download-count')!.textContent!.trim()).toBe('4');
    });

    it('shows a toast when the download fails', () => {
      stubDownload();
      const ctx = boot();

      action(ctx.el, 'Jan Kowalski', 'Pobierz').click();
      ctx.http.expectOne(r => r.method === 'GET' && r.url === `${url}/d1/download`).flush(new Blob(['x']), { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się pobrać dokumentu.');
    });

    it('deletes a document after confirmation and reloads the history', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      action(ctx.el, 'Jan Kowalski', 'Usuń').click();

      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/d1`).flush(null, { status: 204, statusText: 'No Content' });
      expect(toastMessages()).toContain('Dokument usunięty.');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([]);
      ctx.fixture.detectChanges();
      expect(textOf(ctx.el)).toContain('Brak wygenerowanych dokumentów.');
    });

    it('does not delete anything when the confirmation is declined', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      action(ctx.el, 'Jan Kowalski', 'Usuń').click();

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows a toast when deleting fails', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      action(ctx.el, 'Jan Kowalski', 'Usuń').click();
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się usunąć dokumentu.');
    });

    it('offers download but not delete to users who cannot generate documents', () => {
      const ctx = setup(DocumentsComponent, { granted: ['Documents.View'] });
      ctx.fixture.detectChanges();
      ctx.http.expectOne(url).flush(history);
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
      ctx.fixture.detectChanges();

      expect(rowOf(ctx.el, 'Jan Kowalski').textContent).toContain('Pobierz');
      expect(rowOf(ctx.el, 'Jan Kowalski').textContent).not.toContain('Usuń');
    });
  });
});

function clickByTextSafe(el: HTMLElement, text: string): boolean {
  try {
    clickByText(el, text);
    return true;
  } catch {
    return false;
  }
}
