import { TestBed } from '@angular/core/testing';
import { HttpHeaders } from '@angular/common/http';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DocumentsComponent } from './documents.component';
import { GeneratedDocument, LetterTemplate } from './generated-document.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, choosePerson, clickByText, setup, textOf } from '../../testing/test-helpers';

const history: GeneratedDocument[] = [{
  id: 'd1', template: null, letterTemplateId: 't1', title: 'Zaświadczenie o chrzcie', canDownloadWord: true, personId: 'p1', personFullName: 'Jan Kowalski',
  generatedByUserId: 'u1', additionalNotes: null, createdAtUtc: '2026-09-22T10:00:00Z', hasStoredFile: true, downloadCount: 3
}];
const oldDocument: GeneratedDocument = { ...history[0], id: 'd2', title: 'Skierowanie do DOK', canDownloadWord: false, hasStoredFile: false, downloadCount: 1 };
const templates: LetterTemplate[] = [
  { id: 't1', name: 'Zaświadczenie o chrzcie', body: '<p>{{wpisz:Data chrztu}}</p>', useLetterhead: true, isActive: true, sortOrder: 0, updatedAtUtc: '', updatedByEmail: null, inputs: ['Data chrztu'], unknownFields: [], usageCount: 1 },
  { id: 't2', name: 'Klauzula RODO', body: '<p>RODO</p>', useLetterhead: false, isActive: true, sortOrder: 1, updatedAtUtc: '', updatedByEmail: null, inputs: [], unknownFields: [], usageCount: 0 }
];
const url = api('/api/documents');
const templatesUrl = api('/api/document-templates');
const pdfHeaders = new HttpHeaders({ 'Content-Disposition': 'attachment; filename="Zaswiadczenie_Kowalski.pdf"', 'X-Missing-Fields': encodeURIComponent('Misja od') });

function boot(items: GeneratedDocument[] = history, granted?: string[]) {
  const ctx = setup(DocumentsComponent, { granted });
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(items);
  ctx.http.match(r => r.url === templatesUrl).forEach(r => r.flush(templates));
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);
const button = (el: HTMLElement, text: string) => Array.from(el.querySelectorAll('button')).find(b => b.textContent!.trim() === text) as HTMLButtonElement;

describe('DocumentsComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('lists generated letters by title, with PDF and Word downloads', () => {
    const { el } = boot([...history, oldDocument]);
    const text = textOf(el);
    expect(text).toContain('Zaświadczenie o chrzcie');
    expect(text).toContain('odtwarzane z aktualnych danych');
    expect(el.querySelectorAll('tbody tr')[0].textContent).toContain('Word');
    expect(el.querySelectorAll('tbody tr')[1].textContent).not.toContain('Word');
  });

  it('says when nothing has been generated yet, and when there are no templates', () => {
    const ctx = setup(DocumentsComponent);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(url).flush([]);
    ctx.http.expectOne(templatesUrl).flush([]);
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Brak wygenerowanych dokumentów.');
    expect(textOf(ctx.el)).toContain('Nie ma aktywnych wzorów pism.');
  });

  it('asks for the typed-in fields, generates PDF and Word, and lists the missing data', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const ctx = boot();
    expect(button(ctx.el, 'Generuj PDF').disabled).toBe(true);
    choosePerson(ctx.fixture, 'personId', 'p1', 'Jan Kowalski');
    const input = ctx.el.querySelector('#input-0') as HTMLInputElement;
    expect(ctx.el.querySelector('label[for="input-0"]')!.textContent).toContain('Data chrztu');
    input.value = ' 1 maja ';
    input.dispatchEvent(new Event('input'));
    const notes = ctx.el.querySelector('#doc-notes') as HTMLTextAreaElement;
    notes.value = 'Do akt';
    notes.dispatchEvent(new Event('input'));
    ctx.fixture.detectChanges();

    button(ctx.el, 'Generuj PDF').click();
    const req = ctx.http.expectOne(`${url}/generate`);
    expect(req.request.body).toEqual({ letterTemplateId: 't1', personId: 'p1', additionalNotes: 'Do akt', inputs: { 'Data chrztu': '1 maja' }, format: 'pdf' });
    req.flush(new Blob(['%PDF']), { headers: pdfHeaders });
    ctx.http.expectOne(url).flush(history);
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('zabrakło danych: Misja od');
    expect(toastMessages().join(' ')).toContain('uzupełnij odręcznie');

    button(ctx.el, 'Generuj Word').click();
    ctx.http.expectOne(`${url}/generate`).flush(new Blob(['x']));
    ctx.http.expectOne(url).flush(history);
    expect(toastMessages()).toContain('Pismo gotowe.');

    button(ctx.el, 'Generuj PDF').click();
    ctx.http.expectOne(`${url}/generate`).flush(new Blob([JSON.stringify({ title: 'Wzór „X” jest wyłączony.' })]), { status: 400, statusText: 'Bad Request' });
    await new Promise(r => setTimeout(r, 0));
    expect(toastMessages()).toContain('Wzór „X” jest wyłączony.');
  });

  it('switches template keeping typed values, and previews the letter for the person', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const ctx = boot();
    const component = ctx.fixture.componentInstance;
    component.inputs['Data chrztu'] = 'x';
    component.selectTemplate('t2');
    expect(component.inputs).toEqual({});
    const select = ctx.el.querySelector('#doc-template') as HTMLSelectElement;
    select.value = 't2';
    select.dispatchEvent(new Event('change'));
    expect(component.templateId).toBe('t2');
    component.selectTemplate('t1');
    component.showPreview();
    ctx.http.expectNone(`${templatesUrl}/preview`);
    choosePerson(ctx.fixture, 'personId', 'p1');
    button(ctx.el, 'Podgląd').click();
    const preview = ctx.http.expectOne(`${templatesUrl}/preview`);
    expect(preview.request.body).toMatchObject({ body: templates[0].body, useLetterhead: true, personId: 'p1', format: 'pdf' });
    preview.flush(new Blob(['%PDF']), { headers: pdfHeaders });
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-pdf-preview iframe')).not.toBeNull();
    expect(textOf(ctx.el)).toContain('Brak danych w polach: Misja od');
    clickByText(ctx.el, 'Zamknij');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-pdf-preview')).toBeNull();

    button(ctx.el, 'Podgląd').click();
    ctx.http.expectOne(`${templatesUrl}/preview`).flush(new Blob(['x']), { status: 500, statusText: 'Error' });
    await new Promise(r => setTimeout(r, 0));
    expect(toastMessages()).toContain('Nie udało się przygotować podglądu.');
  });

  it('downloads and deletes from the history', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const ctx = boot();
    clickByText(ctx.el, 'Word', '.letter-actions .link');
    ctx.http.expectOne(r => r.url === `${url}/d1/download` && r.params.get('format') === 'docx').flush(new Blob(['x']));
    ctx.http.expectOne(url).flush(history);
    clickByText(ctx.el, 'PDF', '.letter-actions .link');
    ctx.http.expectOne(r => r.url === `${url}/d1/download`).flush(new Blob(['nie json']), { status: 400, statusText: 'Bad Request' });
    await new Promise(r => setTimeout(r, 0));
    expect(toastMessages()).toContain('Nie udało się pobrać dokumentu.');

    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
    clickByText(ctx.el, 'Usuń', '.letter-actions .link');
    ctx.http.expectNone(`${url}/d1`);
    clickByText(ctx.el, 'Usuń', '.letter-actions .link');
    ctx.http.expectOne(r => r.method === 'DELETE').flush(null);
    ctx.http.expectOne(url).flush([]);
    expect(toastMessages()).toContain('Dokument usunięty.');
    ctx.fixture.detectChanges();
  });

  it('reports a failed delete and failed templates load', () => {
    const ctx = setup(DocumentsComponent);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(url).flush(history);
    ctx.http.expectOne(templatesUrl).flush('x', { status: 500, statusText: 'Error' });
    ctx.fixture.detectChanges();
    expect(toastMessages()).toContain('Nie udało się wczytać wzorów pism.');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    clickByText(ctx.el, 'Usuń', '.letter-actions .link');
    ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Error' });
    expect(toastMessages()).toContain('Nie udało się usunąć dokumentu.');
  });

  it('shows the template and letterhead tabs only to those who may edit templates', () => {
    const viewer = boot(history, ['Documents.View']);
    expect(viewer.el.querySelector('.tabs')).toBeNull();
    expect(textOf(viewer.el)).not.toContain('Nowe pismo');
    TestBed.resetTestingModule();

    const editor = boot();
    clickByText(editor.el, 'Wzory pism', '.tabs button');
    editor.fixture.detectChanges();
    expect(editor.el.querySelector('app-letter-templates')).not.toBeNull();
    clickByText(editor.el, 'Nagłówek i stopka', '.tabs button');
    editor.fixture.detectChanges();
    expect(editor.el.querySelector('app-letterhead-settings')).not.toBeNull();
    clickByText(editor.el, 'Pisma', '.tabs button');
    editor.http.match(r => r.url === templatesUrl).forEach(r => r.flush(templates));
    editor.fixture.detectChanges();
    expect(textOf(editor.el)).toContain('Nowe pismo');
  });
});
