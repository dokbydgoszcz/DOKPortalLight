import { TestBed } from '@angular/core/testing';
import { HttpHeaders } from '@angular/common/http';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LetterTemplatesComponent } from './letter-templates.component';
import { LetterTemplate } from './generated-document.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, choosePerson, setup, textOf } from '../../testing/test-helpers';

const t = api('/api/document-templates');
const templates: LetterTemplate[] = [
  { id: 't1', name: 'Zaświadczenie o chrzcie', body: '<p>{{osoba.imię}} {{wpisz:Data chrztu}}</p>', useLetterhead: true, isActive: true, sortOrder: 0,
    updatedAtUtc: '2026-10-16T08:00:00Z', updatedByEmail: 'a@example.org', inputs: ['Data chrztu'], unknownFields: [], usageCount: 2 },
  { id: 't2', name: 'Stary wzór', body: '<p>{{osoba.imie}}</p>', useLetterhead: false, isActive: false, sortOrder: 1,
    updatedAtUtc: '2026-10-16T08:00:00Z', updatedByEmail: null, inputs: [], unknownFields: ['osoba.imie'], usageCount: 0 }
];
const fields = [
  { key: 'osoba.imię', label: 'Imię', group: 'Osoba', example: 'Jan' },
  { key: 'pismo.data', label: 'Data', group: 'Pismo', example: '16.10.2026' }
];

function boot(items = templates) {
  const ctx = setup(LetterTemplatesComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(`${t}/fields`).flush(fields);
  ctx.http.expectOne(r => r.url === t && r.params.get('all') === 'true').flush(items);
  ctx.fixture.detectChanges();
  return ctx;
}

const toasts = () => TestBed.inject(ToastService).toasts().map(m => m.message);
const button = (el: HTMLElement, text: string) => Array.from(el.querySelectorAll('button')).find(b => b.textContent!.trim() === text) as HTMLButtonElement;

describe('LetterTemplatesComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('lists the templates and opens the first one with its fields to type in', async () => {
    const { el, fixture } = boot();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(textOf(el)).toContain('wyłączony');
    expect(textOf(el)).toContain('⚠ pola');
    expect(textOf(el)).toContain('2 pism');
    expect((el.querySelector('#tpl-name') as HTMLInputElement).value).toBe('Zaświadczenie o chrzcie');
    expect(textOf(el)).toContain('portal zapyta o: Data chrztu');
    expect(el.querySelector('.ProseMirror')!.textContent).toContain('{{osoba.imię}}');
    expect(textOf(el)).toContain('wygenerowano 2 pism');
    const name = el.querySelector('#tpl-name') as HTMLInputElement;
    name.value = 'Nowa nazwa';
    name.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.draft()!.name).toBe('Nowa nazwa');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    button(el, 'Powiel').click();
    expect(fixture.componentInstance.draft()!.name).toBe('Zaświadczenie o chrzcie (kopia)');
  });

  it('warns about unknown fields, inserts fields and saves the changes', () => {
    const ctx = boot();
    const component = ctx.fixture.componentInstance;
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
    component.update({ name: 'Zmieniony' });
    (ctx.el.querySelectorAll('.template-list button')[1] as HTMLButtonElement).click();
    expect(component.selectedId()).toBe('t1');
    (ctx.el.querySelectorAll('.template-list button')[1] as HTMLButtonElement).click();
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Nieznane pola: osoba.imie');

    const select = ctx.el.querySelector('select[name="field"]') as HTMLSelectElement;
    select.value = 'pismo.data';
    select.dispatchEvent(new Event('change'));
    component.insertField('');
    vi.spyOn(window, 'prompt').mockReturnValueOnce(' Data {ślubu} ').mockReturnValueOnce('');
    component.insertInput();
    component.insertInput();
    ctx.fixture.detectChanges();
    expect(component.draft()!.body).toContain('{{pismo.data}}');
    expect(component.draft()!.body).toContain('{{wpisz:Data ślubu}}');

    tickBox(ctx.el, 'isActive', true);
    tickBox(ctx.el, 'useLetterhead', true);
    ctx.fixture.detectChanges();
    button(ctx.el, 'Zapisz').click();
    const put = ctx.http.expectOne(`${t}/t2`);
    expect(put.request.body).toMatchObject({ name: 'Stary wzór', isActive: true, useLetterhead: true });
    put.flush(templates[1]);
    ctx.http.expectOne(r => r.url === t).flush(templates);
    expect(toasts()).toContain('Zapisano wzór.');
  });

  it('creates a new template, duplicates one and refuses an empty name', () => {
    const ctx = boot();
    const component = ctx.fixture.componentInstance;
    button(ctx.el, '＋ Nowy').click();
    ctx.fixture.detectChanges();
    expect(component.draft()!.name).toBe('Nowy wzór');
    expect(button(ctx.el, 'Porzuć')).toBeTruthy();
    component.update({ name: '  ' });
    component.save();
    expect(toasts()).toContain('Podaj nazwę wzoru.');
    component.update({ name: 'Podziękowanie' });
    component.save();
    const post = ctx.http.expectOne(r => r.url === t && r.method === 'POST');
    post.flush({ ...templates[0], id: 't3', name: 'Podziękowanie' });
    ctx.http.expectOne(r => r.url === t).flush([...templates, { ...templates[0], id: 't3', name: 'Podziękowanie' }]);
    expect(toasts()).toContain('Dodano wzór.');
    expect(component.selectedId()).toBe('t3');

    component.startNew(templates[0]);
    expect(component.draft()!.name).toBe('Zaświadczenie o chrzcie (kopia)');
    component.save();
    ctx.http.expectOne(r => r.url === t && r.method === 'POST').flush({ title: 'Wzór nie ma treści.' }, { status: 400, statusText: 'Bad Request' });
    expect(toasts()).toContain('Wzór nie ma treści.');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.remove();
    ctx.http.expectOne(r => r.url === t).flush(templates);
  });

  it('deletes a template, or explains why it cannot', () => {
    const ctx = boot();
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
    button(ctx.el, 'Usuń').click();
    ctx.http.expectNone(`${t}/t1`);
    button(ctx.el, 'Usuń').click();
    ctx.http.expectOne(`${t}/t1`).flush({ title: 'Z tego wzoru wygenerowano już 2 pism.' }, { status: 400, statusText: 'Bad Request' });
    expect(toasts()).toContain('Z tego wzoru wygenerowano już 2 pism.');
    button(ctx.el, 'Usuń').click();
    ctx.http.expectOne(`${t}/t1`).flush(null);
    ctx.http.expectOne(r => r.url === t).flush([]);
    ctx.fixture.detectChanges();
    expect(toasts()).toContain('Usunięto wzór.');
    expect(textOf(ctx.el)).toContain('Brak wzorów');
  });

  it('previews the draft as PDF (on a chosen person) or downloads it as Word', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const ctx = boot();
    button(ctx.el, 'Podgląd PDF').click();
    const examples = ctx.http.expectOne(`${t}/preview`);
    expect(examples.request.body).toMatchObject({ personId: null, format: 'pdf', useLetterhead: true });
    examples.flush(new Blob(['%PDF']), { headers: new HttpHeaders({ 'X-Missing-Fields': encodeURIComponent('Data chrztu') }) });
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-pdf-preview')).not.toBeNull();
    (ctx.el.querySelector('app-pdf-preview .close') as HTMLButtonElement).click();
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-pdf-preview')).toBeNull();

    choosePerson(ctx.fixture, 'previewPersonId', 'p1');
    button(ctx.el, 'Pobierz Word').click();
    const word = ctx.http.expectOne(`${t}/preview`);
    expect(word.request.body).toMatchObject({ personId: 'p1', format: 'docx' });
    word.flush(new Blob(['x']));
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();

    button(ctx.el, 'Podgląd PDF').click();
    ctx.http.expectOne(`${t}/preview`).flush(new Blob([JSON.stringify({ title: 'Nie znaleziono wybranej osoby.' })]), { status: 400, statusText: 'Bad Request' });
    await new Promise(r => setTimeout(r, 0));
    expect(toasts()).toContain('Nie znaleziono wybranej osoby.');
  });

  it('copes with failing lists and an empty start', () => {
    const ctx = setup(LetterTemplatesComponent);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(`${t}/fields`).flush('x', { status: 500, statusText: 'Error' });
    ctx.http.expectOne(r => r.url === t).flush('x', { status: 500, statusText: 'Error' });
    ctx.fixture.detectChanges();
    expect(toasts()).toContain('Nie udało się wczytać wzorów pism.');
    const component = ctx.fixture.componentInstance;
    component.update({ name: 'x' });
    component.save();
    component.showPreview();
    component.insertField('osoba.imię');
    expect(component.draft()).toBeNull();
  });
});

function tickBox(el: HTMLElement, name: string, checked: boolean) {
  const box = el.querySelector(`input[name="${name}"]`) as HTMLInputElement;
  box.checked = checked;
  box.dispatchEvent(new Event('change'));
}
