import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { PeopleImportComponent, PeopleImportResult } from './people-import.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, setup, textOf } from '../../testing/test-helpers';

const result: PeopleImportResult = {
  applied: false, newPeople: 1, existingPeople: 1, functionsGranted: 1, candidatesEnrolled: 1, skippedLines: ['Nazwisko i imię'],
  rows: [
    { line: 2, firstName: 'Joanna', lastName: 'Ambrozik', email: 'j@example.org', phone: null, newPerson: true, hadFunction: false, candidateYear: 3, warnings: [] },
    { line: 3, firstName: 'Szymon', lastName: 'Łuczak', email: null, phone: null, newPerson: false, hadFunction: true, candidateYear: null, warnings: ['Jest już kandydatem SKŚP (rok 2) – bez zmian.'] }
  ]
};

function boot() {
  const ctx = setup(PeopleImportComponent);
  ctx.fixture.detectChanges();
  return ctx;
}

const button = (el: HTMLElement, text: string) => Array.from(el.querySelectorAll('button')).find(b => b.textContent!.trim() === text) as HTMLButtonElement;
const set = (el: HTMLElement, selector: string, value: string) => {
  const input = el.querySelector(selector) as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input'));
};
const tick = (el: HTMLElement, selector: string, event = 'change', checked = true) => {
  const input = el.querySelector(selector) as HTMLInputElement;
  input.checked = checked;
  input.dispatchEvent(new Event(event));
};

describe('PeopleImportComponent', () => {
  it('checks the pasted list, shows the preview and imports with the chosen function and SKŚP year', () => {
    const ctx = boot();
    let imported: PeopleImportResult | null = null;
    ctx.fixture.componentInstance.imported.subscribe(r => (imported = r));
    expect(button(ctx.el, 'Sprawdź').disabled).toBe(true);

    set(ctx.el, 'textarea', 'Nazwisko i imię\n1 Ambrozik Joanna\n2 Łuczak Szymon');
    set(ctx.el, 'input[name="note"]', ' Absolwent SKŚP ');
    tick(ctx.el, 'input[name="enroll"]');
    ctx.fixture.detectChanges();
    const year = ctx.el.querySelector('select[name="year"]') as HTMLSelectElement;
    year.value = '3';
    year.dispatchEvent(new Event('change'));
    const fn = ctx.el.querySelector('select[name="function"]') as HTMLSelectElement;
    fn.value = 'Lector';
    fn.dispatchEvent(new Event('change'));
    tick(ctx.el, 'input[name="order"]:nth-of-type(1)');
    ctx.fixture.detectChanges();
    button(ctx.el, 'Sprawdź').click();

    const req = ctx.http.expectOne(api('/api/people/import/preview'));
    expect(req.request.body).toEqual({
      text: 'Nazwisko i imię\n1 Ambrozik Joanna\n2 Łuczak Szymon', firstNameFirst: false, function: 'Lector', functionNote: 'Absolwent SKŚP',
      enrollAsCandidates: true, candidateYear: 3
    });
    req.flush(result);
    ctx.fixture.detectChanges();
    const text = textOf(ctx.el);
    expect(text).toContain('nowe: 1');
    expect(text).toContain('Pominięte wiersze (nagłówki): Nazwisko i imię');
    expect(text).toContain('＋ kandydat SKŚP, rok 3');
    expect(text).toContain('ma już funkcję');
    expect(text).toContain('⚠ Jest już kandydatem');

    button(ctx.el, 'Importuj').click();
    ctx.http.expectOne(api('/api/people/import')).flush({ ...result, applied: true });
    expect(TestBed.inject(ToastService).toasts().map(t => t.message))
      .toContain('Zaimportowano: 1 nowych osób, 1 nadanych funkcji, 1 nowych kandydatów SKŚP.');
    expect(imported).not.toBeNull();
  });

  it('forgets the preview when the list changes, sends no function when unticked, and reports errors', () => {
    const ctx = boot();
    let closed = false;
    ctx.fixture.componentInstance.closed.subscribe(() => (closed = true));
    set(ctx.el, 'textarea', 'Jan Nowak');
    tick(ctx.el, 'input[name="grant"]', 'change', false);
    const order = ctx.el.querySelectorAll('input[name="order"]')[1] as HTMLInputElement;
    order.checked = true;
    order.dispatchEvent(new Event('change'));
    ctx.fixture.detectChanges();
    button(ctx.el, 'Sprawdź').click();
    const req = ctx.http.expectOne(api('/api/people/import/preview'));
    expect(req.request.body).toMatchObject({ firstNameFirst: true, function: null, functionNote: null, enrollAsCandidates: false, candidateYear: null });
    req.flush({ ...result, skippedLines: [] });
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).not.toContain('nadanie funkcji');

    set(ctx.el, 'textarea', 'Jan Nowak\nAla Kot');
    ctx.fixture.detectChanges();
    expect(button(ctx.el, 'Importuj').disabled).toBe(true);

    button(ctx.el, 'Sprawdź').click();
    ctx.http.expectOne(api('/api/people/import/preview')).flush({ title: 'Lista jest pusta.' }, { status: 400, statusText: 'Bad Request' });
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Lista jest pusta.');

    ctx.fixture.componentInstance.run(true);
    ctx.http.expectOne(api('/api/people/import')).flush('x', { status: 500, statusText: 'Error' });
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Import się nie udał.');
    button(ctx.el, 'Anuluj').click();
    (ctx.el.querySelector('.close') as HTMLButtonElement).click();
    expect(closed).toBe(true);
  });
});
