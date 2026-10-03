import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { PersonFormComponent } from './person-form.component';
import { PersonFormValue } from './person.model';
import { clickByText, setInput } from '../../testing/test-helpers';

function render(open: boolean, value: PersonFormValue = { firstName: '', lastName: '' }) {
  TestBed.configureTestingModule({ imports: [PersonFormComponent] });
  const fixture = TestBed.createComponent(PersonFormComponent);
  fixture.componentRef.setInput('open', open);
  fixture.componentRef.setInput('value', value);
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
});
