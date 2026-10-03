import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DocumentsComponent } from './documents.component';
import { GeneratedDocument } from './generated-document.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, paged, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const history: GeneratedDocument[] = [{
  id: 'd1', template: 'LetterToBishop', personId: 'p1', personFullName: 'Jan Kowalski',
  generatedByUserId: 'u1', additionalNotes: null, createdAtUtc: '2026-09-22T10:00:00Z'
}];
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
});

function clickByTextSafe(el: HTMLElement, text: string): boolean {
  try {
    clickByText(el, text);
    return true;
  } catch {
    return false;
  }
}
