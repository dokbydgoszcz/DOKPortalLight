import { describe, expect, it } from 'vitest';
import { CandidateFormComponent } from './candidate-form.component';
import { CandidateFormValue } from './candidate.model';
import { api, clickByText, paged, setInput, setSelect, setSelectByLabel, setup } from '../../testing/test-helpers';

const people = [
  { id: 'p1', firstName: 'Jan', lastName: 'Kowalski', fullName: 'Jan Kowalski', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];

function render(open: boolean) {
  const ctx = setup(CandidateFormComponent);
  const value: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, retreats: [] };
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

    expect(saved).toEqual([{ personId: 'p1', year: 3, attendancePercentage: 90, opinionsCollected: 2, retreats: [] }]);
  });

  it('emits cancel from the footer button and the close icon', () => {
    const { el, cancelled } = render(true);

    clickByText(el, 'Anuluj', '.modal-foot button');
    (el.querySelector('.modal-head .close') as HTMLButtonElement).click();

    expect(cancelled()).toBe(2);
  });

  describe('retreats', () => {
    const retreatSelect = (el: HTMLElement, year: number) => el.querySelector(`select[name="retreat${year}"]`) as HTMLSelectElement;

    it('offers one status per formation year, empty by default', async () => {
      const { fixture, el } = render(true);
      await fixture.whenStable();

      for (const year of [1, 2, 3]) {
        const select = retreatSelect(el, year);
        expect(select).not.toBeNull();
        expect(Array.from(select.options).map(o => o.textContent!.trim())).toEqual(['—', 'oczekuje', 'zaliczone']);
        expect(select.value).toBe('');
      }
    });

    it('emits only the years that were given a status', async () => {
      const { fixture, el, saved } = render(true);
      await fixture.whenStable();
      setSelect(el, 'select[name="personId"]', 'p1');

      setSelectByLabel(el, 'select[name="retreat1"]', 'zaliczone');
      setSelectByLabel(el, 'select[name="retreat3"]', 'oczekuje');
      fixture.detectChanges();
      saveButton(el).click();

      expect(saved[0].retreats).toEqual([{ year: 1, isCompleted: true }, { year: 3, isCompleted: false }]);
    });

    it('shows the retreats of the edited candidate and lets you clear one', async () => {
      const ctx = setup(CandidateFormComponent);
      ctx.fixture.componentRef.setInput('open', false);
      ctx.fixture.componentRef.setInput('editing', true);
      ctx.fixture.componentRef.setInput('value', {
        personId: 'p1', year: 2, opinionsCollected: 1, retreats: [{ year: 1, isCompleted: true }, { year: 2, isCompleted: false }]
      } satisfies CandidateFormValue);
      const saved: CandidateFormValue[] = [];
      ctx.fixture.componentInstance.save.subscribe(v => saved.push(v));
      ctx.fixture.detectChanges();
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
      ctx.fixture.componentRef.setInput('open', true);
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      expect(ctx.el.textContent).toContain('Edytuj kandydata');
      expect(retreatSelect(ctx.el, 1).value).toBe('done');
      expect(retreatSelect(ctx.el, 2).value).toBe('pending');
      expect(retreatSelect(ctx.el, 3).value).toBe('');

      setSelectByLabel(ctx.el, 'select[name="retreat1"]', '—');
      ctx.fixture.detectChanges();
      saveButton(ctx.el).click();

      expect(saved[0].retreats).toEqual([{ year: 2, isCompleted: false }]);
    });

    it('starts over from the new value when the form is reopened for another candidate', async () => {
      const ctx = setup(CandidateFormComponent);
      ctx.fixture.componentRef.setInput('open', true);
      ctx.fixture.componentRef.setInput('value', { personId: 'p1', year: 1, opinionsCollected: 0, retreats: [{ year: 1, isCompleted: true }] } satisfies CandidateFormValue);
      ctx.fixture.detectChanges();
      ctx.http.expectOne(r => r.url === api('/api/people')).flush(paged(people));
      await ctx.fixture.whenStable();
      expect(retreatSelect(ctx.el, 1).value).toBe('done');

      ctx.fixture.componentRef.setInput('value', { personId: 'p1', year: 1, opinionsCollected: 0, retreats: [] } satisfies CandidateFormValue);
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();

      expect(retreatSelect(ctx.el, 1).value).toBe('');
    });
  });

  it('does not send a leftover reason when the formation is not stopped', async () => {
    const { fixture, el, saved } = render(true);
    await fixture.whenStable();
    setSelect(el, 'select[name="personId"]', 'p1');
    fixture.componentInstance.value.formationStopNote = 'stara notatka';
    fixture.detectChanges();

    saveButton(el).click();

    expect(saved[0].isFormationStopped).toBeFalsy();
    expect(saved[0].formationStopNote).toBeUndefined();
  });
});
