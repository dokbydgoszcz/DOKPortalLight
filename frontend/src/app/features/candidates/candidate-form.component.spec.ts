import { describe, expect, it } from 'vitest';
import { CandidateFormComponent } from './candidate-form.component';
import { CandidateFormValue } from './candidate.model';
import { api, clickByText, paged, setInput, setSelect, setSelectByLabel, setup } from '../../testing/test-helpers';

const people = [
  { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];

function render(open: boolean) {
  const ctx = setup(CandidateFormComponent);
  const value: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };
  ctx.fixture.componentRef.setInput('open', false);
  ctx.fixture.componentRef.setInput('value', value);
  const saved: CandidateFormValue[] = [];
  let cancelled = 0;
  ctx.fixture.componentInstance.save.subscribe(v => saved.push(v));
  ctx.fixture.componentInstance.cancel.subscribe(() => cancelled++);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
  ctx.fixture.componentRef.setInput('open', open);
  ctx.fixture.detectChanges();
  return { ...ctx, saved, cancelled: () => cancelled };
}

const saveButton = (el: HTMLElement) =>
  Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button')).find(b => b.textContent!.includes('Zapisz'))!;

describe('CandidateFormComponent', () => {
  it('renders nothing while closed', () => {
    const { el } = render(false);

    expect(el.querySelector('.modal')).toBeNull();
  });

  it('keeps saving disabled until a person is chosen, then emits the typed values', async () => {
    const { fixture, el, saved } = render(true);
    await fixture.whenStable();
    expect(saveButton(el).disabled).toBe(true);

    setSelect(el, 'select[name="personId"]', 'p1');
    setSelectByLabel(el, 'select[name="year"]', 'III ROK');
    setInput(el, 'input[name="attendancePercentage"]', '90');
    setInput(el, 'input[name="opinionsCollected"]', '2');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(false);
    saveButton(el).click();

    expect(saved).toEqual([{ personId: 'p1', year: 3, attendancePercentage: 90, opinionsCollected: 2, isRetreatCompleted: false }]);
  });

  it('emits cancel from the footer button and the close icon', () => {
    const { el, cancelled } = render(true);

    clickByText(el, 'Anuluj', '.modal-foot button');
    (el.querySelector('.modal-head .close') as HTMLButtonElement).click();

    expect(cancelled()).toBe(2);
  });
});
