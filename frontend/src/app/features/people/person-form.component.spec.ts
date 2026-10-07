import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { PersonFormComponent } from './person-form.component';
import { Parish } from '../parish-board/parish-need.model';
import { PersonFormValue } from './person.model';
import { clickByText, setInput, setSelect } from '../../testing/test-helpers';

const parishes: Parish[] = [
  { id: 'p1', name: 'św. Jana', city: 'Bydgoszcz' },
  { id: 'p2', name: 'św. Pawła', city: null }
];

function render(open: boolean, value: PersonFormValue = { firstName: '', lastName: '' }) {
  TestBed.configureTestingModule({ imports: [PersonFormComponent] });
  const fixture = TestBed.createComponent(PersonFormComponent);
  fixture.componentRef.setInput('open', open);
  fixture.componentRef.setInput('value', value);
  fixture.componentRef.setInput('parishes', parishes);
  const saved: PersonFormValue[] = [];
  let cancelled = 0;
  fixture.componentInstance.save.subscribe(v => saved.push(v));
  fixture.componentInstance.cancel.subscribe(() => cancelled++);
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement as HTMLElement, saved, cancelled: () => cancelled };
}

describe('PersonFormComponent', () => {
  it('renders nothing while closed', () => {
    const { el } = render(false);

    expect(el.querySelector('.modal')).toBeNull();
  });

  it('shows the initial values and emits the edited value on save', async () => {
    const { fixture, el, saved } = render(true, { firstName: 'Anna', lastName: 'Maj', email: 'anna@example.org' });
    await fixture.whenStable();

    expect((el.querySelector('input[name="firstName"]') as HTMLInputElement).value).toBe('Anna');
    expect((el.querySelector('input[name="email"]') as HTMLInputElement).value).toBe('anna@example.org');

    setInput(el, 'input[name="lastName"]', 'Nowak');
    clickByText(el, 'Zapisz', '.modal-foot button');

    expect(saved).toEqual([{ firstName: 'Anna', lastName: 'Nowak', email: 'anna@example.org' }]);
  });

  it('emits cancel from both the footer button and the close icon', () => {
    const { el, cancelled } = render(true);

    clickByText(el, 'Anuluj', '.modal-foot button');
    (el.querySelector('.modal-head .close') as HTMLButtonElement).click();

    expect(cancelled()).toBe(2);
  });

  it('currently lets an empty form be saved (the backend validates it)', () => {
    const { el, saved } = render(true);

    clickByText(el, 'Zapisz', '.modal-foot button');

    expect(saved).toEqual([{ firstName: '', lastName: '' }]);
  });

  describe('functions', () => {
    it('adds a function row, lets a pastor choose a parish and emits the functions', async () => {
      const { fixture, el, saved } = render(true, { firstName: 'Ks. Jan', lastName: 'Kowalski' });
      await fixture.whenStable();

      clickByText(el, 'Dodaj funkcję');
      fixture.detectChanges();
      await fixture.whenStable();
      setSelect(el, 'select[name="fnType0"]', 'Pastor');
      fixture.detectChanges();
      await fixture.whenStable();
      setSelect(el, 'select[name="fnParish0"]', 'p2');
      setInput(el, 'input[name="fnDate0"]', '2020-05-01');
      setInput(el, 'input[name="fnNotes0"]', 'Od września');
      clickByText(el, 'Zapisz', '.modal-foot button');

      expect(saved[0].functions).toEqual([{ type: 'Pastor', parishId: 'p2', institutedOn: '2020-05-01', notes: 'Od września' }]);
    });

    it('offers the parish only for a pastor', async () => {
      const { fixture, el } = render(true, { firstName: 'A', lastName: 'B', functions: [{ type: 'Acolyte' }] });
      await fixture.whenStable();

      expect(el.querySelector('select[name="fnParish0"]')).toBeNull();
    });

    it('shows the existing functions, removes one and drops empty dates and notes', async () => {
      const { fixture, el, saved } = render(true, {
        firstName: 'A', lastName: 'B', functions: [{ type: 'Acolyte', institutedOn: '', notes: '' }, { type: 'Lector' }]
      });
      await fixture.whenStable();
      expect((el.querySelector('select[name="fnType1"]') as HTMLSelectElement).value).toBe('Lector');

      (el.querySelectorAll('.function-row')[1].querySelector('button.remove') as HTMLButtonElement).click();
      fixture.detectChanges();
      clickByText(el, 'Zapisz', '.modal-foot button');

      expect(saved[0].functions).toEqual([{ type: 'Acolyte' }]);
      expect(Object.keys(saved[0].functions![0])).toEqual(['type']);
    });

    it('clears the parish when the function is no longer a pastor', async () => {
      const { fixture, el, saved } = render(true, { firstName: 'A', lastName: 'B', functions: [{ type: 'Pastor', parishId: 'p1' }] });
      await fixture.whenStable();

      setSelect(el, 'select[name="fnType0"]', 'Lector');
      fixture.detectChanges();
      clickByText(el, 'Zapisz', '.modal-foot button');

      expect(saved[0].functions).toEqual([{ type: 'Lector' }]);
    });

    it('proposes the first function that is not yet on the list', async () => {
      const { fixture, el, saved } = render(true, { firstName: 'A', lastName: 'B', functions: [{ type: 'Catechist' }] });
      await fixture.whenStable();

      clickByText(el, 'Dodaj funkcję');
      clickByText(el, 'Zapisz', '.modal-foot button');

      expect(saved[0].functions!.map(f => f.type)).toEqual(['Catechist', 'Acolyte']);
    });
  });
});
