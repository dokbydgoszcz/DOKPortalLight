import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SkspClassesComponent } from './sksp-classes.component';
import { SkspClass, presenceSummary } from './sksp-class.model';
import { ToastService } from '../../core/notifications/toast.service';
import { api, clickByText, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const url = api('/api/sksp-classes');
const liturgy: SkspClass = {
  id: 'k1', year: 2, classDate: '2026-10-17', startTime: '09:00:00', endTime: '13:00:00', topic: 'Liturgia', notes: 'sala 3',
  attendees: [
    { candidateId: 'c1', personFullName: 'Jan Adamski', isPresent: true },
    { candidateId: 'c2', personFullName: 'Ewa Bielska', isPresent: null }
  ]
};

const toasts = () => TestBed.inject(ToastService).toasts().map(t => t.message);
const saveButton = (el: HTMLElement) =>
  Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

function boot(classes: SkspClass[] = [liturgy], granted = ['*']) {
  const ctx = setup(SkspClassesComponent, { granted });
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(classes);
  ctx.fixture.detectChanges();
  return ctx;
}

describe('SkspClassesComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('lists the classes with hours, year and a presence summary', () => {
    const { el } = boot();
    const text = textOf(el);
    expect(text).toContain('17.10.2026');
    expect(text).toContain('09:00–13:00');
    expect(text).toContain('II rok');
    expect(text).toContain('Liturgia');
    expect(text).toContain('obecni 1/2');
  });

  it('says how to start when there are no classes', () => {
    expect(textOf(boot([]).el)).toContain('Brak zajęć.');
  });

  it('shows a toast when loading fails', () => {
    const ctx = setup(SkspClassesComponent);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' });
    expect(toasts()).toContain('Nie udało się wczytać zajęć SKŚP.');
  });

  it('filters by year', async () => {
    const ctx = boot();
    await ctx.fixture.whenStable();
    setSelect(ctx.el, 'select[name="yearFilter"]', '3: 3');
    ctx.http.expectOne(r => r.url === url && r.params.get('year') === '3').flush([]);
  });

  it('marks attendance, toggles it off again and marks the rest present', () => {
    const ctx = boot();
    clickByText(ctx.el, 'obecni 1/2');
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('Ewa Bielska');

    const row = (name: string) => Array.from(ctx.el.querySelectorAll<HTMLElement>('.attendees li')).find(li => li.textContent!.includes(name))!;
    clickByText(row('Ewa Bielska'), 'Nieobecny', 'button');
    let req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/k1/attendance`);
    expect(req.request.body).toEqual([{ candidateId: 'c2', isPresent: false }]);
    req.flush({ ...liturgy, attendees: [liturgy.attendees[0], { ...liturgy.attendees[1], isPresent: false }] });

    clickByText(row('Jan Adamski'), 'Obecny', 'button');
    req = ctx.http.expectOne(r => r.method === 'PUT');
    expect(req.request.body).toEqual([{ candidateId: 'c1', isPresent: null }]);
    req.flush('x', { status: 500, statusText: 'Server Error' });
    expect(toasts()).toContain('Nie udało się zapisać obecności.');

    ctx.fixture.componentInstance.markRestPresent(liturgy);
    req = ctx.http.expectOne(r => r.method === 'PUT');
    expect(req.request.body).toEqual([{ candidateId: 'c2', isPresent: true }]);
    req.flush('x', { status: 500, statusText: 'Server Error' });

    ctx.fixture.componentInstance.markRestPresent({ ...liturgy, attendees: [liturgy.attendees[0]] });
    ctx.http.expectNone(r => r.method === 'PUT');

    clickByText(ctx.el, 'obecni', '.link');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('.attendees')).toBeNull();
  });

  it('adds late joiners to the list and says how many', () => {
    const ctx = boot();
    ctx.fixture.componentInstance.toggle(liturgy);
    ctx.fixture.detectChanges();
    clickByText(ctx.el, 'Dopisz nowych kandydatów');
    ctx.http.expectOne(`${url}/k1/refresh-attendees`).flush({ ...liturgy, attendees: [...liturgy.attendees, { candidateId: 'c3', personFullName: 'Nowy', isPresent: null }] });
    expect(toasts()).toContain('Dopisano do listy: 1 os.');

    ctx.fixture.componentInstance.refresh(liturgy);
    ctx.http.expectOne(`${url}/k1/refresh-attendees`).flush(liturgy);
    expect(toasts()).toContain('Lista obecności jest aktualna.');

    ctx.fixture.componentInstance.refresh(liturgy);
    ctx.http.expectOne(`${url}/k1/refresh-attendees`).flush('x', { status: 500, statusText: 'Server Error' });
    expect(toasts()).toContain('Nie udało się uzupełnić listy.');
  });

  it('adds classes for a year with default hours and opens their attendance list', async () => {
    const ctx = boot([]);
    clickByText(ctx.el, 'Dodaj zajęcia');
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    expect(saveButton(ctx.el).disabled).toBe(true);
    expect(textOf(ctx.el)).toContain('Na listę obecności trafią kandydaci');

    setSelect(ctx.el, 'select[name="year"]', '1: 2');
    setInput(ctx.el, 'input[name="classDate"]', '2026-10-17');
    setInput(ctx.el, 'input[name="topic"]', 'Liturgia');
    ctx.fixture.detectChanges();
    saveButton(ctx.el).click();

    const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
    expect(req.request.body).toEqual({ year: 2, classDate: '2026-10-17', startTime: '18:00', endTime: '20:00', topic: 'Liturgia' });
    req.flush(liturgy);
    expect(toasts()).toContain('Dodano zajęcia – na liście obecności: 2 os.');
    expect(ctx.fixture.componentInstance.expandedId()).toBe('k1');
    ctx.http.expectOne(url).flush([liturgy]);
  });

  it('refuses to save bad hours and reports server errors', () => {
    const ctx = boot([]);
    const component = ctx.fixture.componentInstance;
    component.openAddForm();
    component.formValue = { year: 1, classDate: '2026-10-17', topic: 'x', endTime: '10:00' };
    expect(component.canSave).toBe(false);
    component.formValue = { year: 1, classDate: '2026-10-17', topic: 'x', startTime: '10:00', endTime: '09:00' };
    expect(component.canSave).toBe(false);
    component.formValue = { year: 1, classDate: '2026-10-17', topic: 'x' };
    component.save();
    ctx.http.expectOne(r => r.method === 'POST').flush({ title: 'Podaj temat zajęć.' }, { status: 400, statusText: 'Bad Request' });
    expect(toasts()).toContain('Podaj temat zajęć.');
  });

  it('edits the details without changing the year', async () => {
    const ctx = boot();
    clickByText(ctx.el, 'Edytuj');
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    expect(ctx.el.querySelector<HTMLSelectElement>('select[name="year"]')!.disabled).toBe(true);
    expect(ctx.el.querySelector<HTMLInputElement>('input[name="startTime"]')!.value).toBe('09:00');

    setInput(ctx.el, 'input[name="topic"]', 'Liturgia godzin');
    saveButton(ctx.el).click();
    const req = ctx.http.expectOne(r => r.method === 'PUT' && r.url === `${url}/k1`);
    expect(req.request.body.topic).toBe('Liturgia godzin');
    req.flush(liturgy);
    expect(toasts()).toContain('Zapisano zmiany.');
    ctx.http.expectOne(url).flush([liturgy]);

    ctx.fixture.componentInstance.openEditForm({ ...liturgy, startTime: null, endTime: null, notes: null });
    ctx.fixture.componentInstance.save();
    ctx.http.expectOne(r => r.method === 'PUT').flush('x', { status: 500, statusText: 'Server Error' });
    expect(toasts()).toContain('Nie udało się zapisać zmian.');
    clickByText(ctx.el, 'Anuluj', '.modal-foot button');
  });

  it('deletes after confirmation', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
    const ctx = boot();
    clickByText(ctx.el, 'Usuń');
    ctx.http.expectNone(r => r.method === 'DELETE');
    expect(confirmSpy.mock.calls[0][0]).toContain('przestanie się liczyć do frekwencji');

    clickByText(ctx.el, 'Usuń');
    ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/k1`).flush(null);
    expect(toasts()).toContain('Zajęcia usunięte.');
    ctx.http.expectOne(url).flush([]);

    ctx.fixture.componentInstance.remove(liturgy);
    ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });
    expect(toasts()).toContain('Nie udało się usunąć zajęć.');
  });

  it('is read-only without the manage permission', () => {
    const ctx = boot([{ ...liturgy, attendees: [...liturgy.attendees, { candidateId: 'c3', personFullName: 'Ola', isPresent: false }] }], ['Candidates.View']);
    expect(textOf(ctx.el)).not.toContain('Dodaj zajęcia');
    expect(textOf(ctx.el)).not.toContain('Edytuj');
    clickByText(ctx.el, 'obecni 1/3');
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelectorAll('.attendees .pill').length).toBe(2);
    expect(ctx.el.querySelector('.attendance-actions')).toBeNull();
  });

  it('summarises presence', () => {
    expect(presenceSummary({ ...liturgy, attendees: [{ ...liturgy.attendees[1] }] })).toBe('obecność nieoznaczona');
  });
});
