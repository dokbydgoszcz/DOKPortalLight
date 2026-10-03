import { describe, expect, it } from 'vitest';
import { MissionFormComponent } from './mission-form.component';
import { MissionFormValue } from './mission.model';
import { api, clickByText, paged, setInput, setSelect, setup } from '../../testing/test-helpers';

const people = [
  { id: 'p1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }
];

function render(open: boolean) {
  const ctx = setup(MissionFormComponent);
  const value: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
  ctx.fixture.componentRef.setInput('open', false);
  ctx.fixture.componentRef.setInput('value', value);
  const saved: MissionFormValue[] = [];
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

describe('MissionFormComponent', () => {
  it('renders nothing while closed', () => {
    const { el } = render(false);

    expect(el.querySelector('.modal')).toBeNull();
  });

  it('keeps saving disabled until a catechist is chosen, then emits the typed values', async () => {
    const { fixture, el, saved } = render(true);
    await fixture.whenStable();
    expect(saveButton(el).disabled).toBe(true);

    setSelect(el, 'select[name="personId"]', 'p1');
    setInput(el, 'input[name="servicePlace"]', 'Parafia św. Jana');
    setInput(el, 'input[name="missionStartDate"]', '2026-01-01');
    setInput(el, 'input[name="missionEndDate"]', '2029-01-01');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(false);
    saveButton(el).click();

    expect(saved).toEqual([{ personId: 'p1', servicePlace: 'Parafia św. Jana', missionStartDate: '2026-01-01', missionEndDate: '2029-01-01' }]);
  });

  it('keeps saving disabled until both mission dates are chosen', async () => {
    const { fixture, el, saved } = render(true);
    await fixture.whenStable();
    setSelect(el, 'select[name="personId"]', 'p1');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(true);

    setInput(el, 'input[name="missionStartDate"]', '2026-01-01');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(true);

    setInput(el, 'input[name="missionEndDate"]', '2029-01-01');
    fixture.detectChanges();
    expect(saveButton(el).disabled).toBe(false);
    expect(saved).toEqual([]);
  });

  it('does not send an emptied optional grant date as an empty string', async () => {
    const { fixture, el, saved } = render(true);
    await fixture.whenStable();
    setSelect(el, 'select[name="personId"]', 'p1');
    setInput(el, 'input[name="missionStartDate"]', '2026-01-01');
    setInput(el, 'input[name="missionEndDate"]', '2029-01-01');
    setInput(el, 'input[name="grantedDate"]', '2025-12-20');
    setInput(el, 'input[name="grantedDate"]', '');
    fixture.detectChanges();

    saveButton(el).click();

    expect(saved).toHaveLength(1);
    expect(saved[0].grantedDate).toBeUndefined();
  });

  it('emits cancel from the footer button and the close icon', () => {
    const { el, cancelled } = render(true);

    clickByText(el, 'Anuluj', '.modal-foot button');
    (el.querySelector('.modal-head .close') as HTMLButtonElement).click();

    expect(cancelled()).toBe(2);
  });
});
