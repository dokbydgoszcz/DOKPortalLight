import { describe, expect, it } from 'vitest';
import { DokCaseFormComponent } from './dok-case-form.component';
import { DokCaseFormValue } from './dok-case.model';
import { api, clickByText, paged, setSelect, setup } from '../../testing/test-helpers';

const people = [
  { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null },
  { id: 'c1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];

function render(open: boolean) {
  const ctx = setup(DokCaseFormComponent);
  const value: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
  ctx.fixture.componentRef.setInput('open', false);
  ctx.fixture.componentRef.setInput('value', value);
  const saved: DokCaseFormValue[] = [];
  let cancelled = 0;
  ctx.fixture.componentInstance.save.subscribe(v => saved.push(v));
  ctx.fixture.componentInstance.cancel.subscribe(() => cancelled++);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  // Like in the app: the people load while the form is closed, opening it (input change) refreshes the view.
  ctx.fixture.componentRef.setInput('open', open);
  ctx.fixture.detectChanges();
  return { ...ctx, saved, cancelled: () => cancelled };
}

const saveButton = (el: HTMLElement) =>
  Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

describe('DokCaseFormComponent', () => {
  it('loads up to 200 people for the selectors and renders nothing while closed', () => {
    const { el } = render(false);

    expect(el.querySelector('.modal')).toBeNull();
  });

  it('lists the people as options for both the person and the catechist', async () => {
    const { fixture, el } = render(true);
    await fixture.whenStable();

    expect(el.querySelectorAll('select[name="personId"] option').length).toBe(3);
    expect(el.querySelectorAll('select[name="catechistPersonId"] option').length).toBe(3);
  });

  it('enables saving only once both a person and a catechist are chosen and emits the chosen values', async () => {
    const { fixture, el, saved } = render(true);
    await fixture.whenStable();
    expect(saveButton(el).disabled).toBe(true);

    setSelect(el, 'select[name="personId"]', 'p1');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(true);

    setSelect(el, 'select[name="catechistPersonId"]', 'c1');
    setSelect(el, 'select[name="path"]', 'Communion');
    setSelect(el, 'select[name="stage"]', 'Sacrament');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(false);

    saveButton(el).click();
    expect(saved).toEqual([{ personId: 'p1', path: 'Communion', stage: 'Sacrament', catechistPersonId: 'c1' }]);
  });

  it('emits cancel from the footer button and the close icon', () => {
    const { el, cancelled } = render(true);

    clickByText(el, 'Anuluj', '.modal-foot button');
    (el.querySelector('.modal-head .close') as HTMLButtonElement).click();

    expect(cancelled()).toBe(2);
  });
});
