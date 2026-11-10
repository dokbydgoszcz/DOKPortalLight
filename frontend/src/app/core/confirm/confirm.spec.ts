import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import { ConfirmService } from './confirm.service';

function boot() {
  TestBed.configureTestingModule({ imports: [ConfirmDialogComponent] });
  const fixture = TestBed.createComponent(ConfirmDialogComponent);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const buttons = () => Array.from(el.querySelectorAll<HTMLButtonElement>('.modal-foot button'));
  return { fixture, el, service: TestBed.inject(ConfirmService), buttons };
}

const key = (name: string, shift = false) => document.dispatchEvent(new KeyboardEvent('keydown', { key: name, shiftKey: shift, cancelable: true }));

describe('ConfirmService with the portal dialog', () => {
  afterEach(() => vi.restoreAllMocks());

  it('asks in its own window and runs the action only after the confirming click', () => {
    const { fixture, el, service, buttons } = boot();
    const native = vi.spyOn(window, 'confirm');
    const done = vi.fn();

    service.ask('Usunąć osobę „Jan Kowalski”?', done);
    fixture.detectChanges();

    expect(native).not.toHaveBeenCalled();
    expect(el.querySelector('[role="alertdialog"]')!.textContent).toContain('Usunąć osobę „Jan Kowalski”?');
    expect(buttons().map(b => b.textContent!.trim())).toEqual(['Anuluj', 'Usuń']);
    expect(buttons()[1].classList.contains('danger-solid')).toBe(true);
    expect(done).not.toHaveBeenCalled();

    buttons()[1].click();
    fixture.detectChanges();
    expect(done).toHaveBeenCalledTimes(1);
    expect(el.querySelector('[role="alertdialog"]')).toBeNull();
    service.answer(true);
    expect(done).toHaveBeenCalledTimes(1);
  });

  it('cancels with the button, Escape or a click outside, and tells the caller', () => {
    const { fixture, el, service, buttons } = boot();
    const done = vi.fn();
    const cancelled = vi.fn();

    service.ask('Wznowić formację?', done, { onCancel: cancelled });
    fixture.detectChanges();
    expect(buttons().map(b => b.textContent!.trim())).toEqual(['Anuluj', 'Wznów']);
    expect(buttons()[1].classList.contains('primary')).toBe(true);
    buttons()[0].click();

    service.ask('Na pewno?', done, { onCancel: cancelled });
    fixture.detectChanges();
    expect(buttons()[1].textContent!.trim()).toBe('Tak');
    key('Escape');

    service.ask('Na pewno?', done, { onCancel: cancelled });
    fixture.detectChanges();
    (el.querySelector('.modal') as HTMLElement).click();
    expect(service.pending()).not.toBeNull();
    (el.querySelector('.overlay') as HTMLElement).click();

    service.ask('Bez reakcji na rezygnację?', done);
    service.answer(false);

    expect(done).not.toHaveBeenCalled();
    expect(cancelled).toHaveBeenCalledTimes(3);
    key('Escape');
    key('Tab');
  });

  it('uses the labels and the danger look the caller chose', () => {
    const { fixture, service, buttons } = boot();
    service.ask('Masz niezapisane zmiany. Porzucić je?', vi.fn(), { confirmLabel: 'Porzuć zmiany', cancelLabel: 'Zostań', danger: true });
    fixture.detectChanges();
    expect(buttons().map(b => b.textContent!.trim())).toEqual(['Zostań', 'Porzuć zmiany']);
    expect(buttons()[1].classList.contains('danger-solid')).toBe(true);
  });

  it('a new question replaces the open one, which counts as cancelled', () => {
    const { fixture, service, buttons } = boot();
    const first = vi.fn();
    const firstCancelled = vi.fn();
    const second = vi.fn();
    service.ask('Usunąć A?', first, { onCancel: firstCancelled });
    service.ask('Usunąć B?', second);
    fixture.detectChanges();
    buttons()[1].click();
    expect([first.mock.calls.length, firstCancelled.mock.calls.length, second.mock.calls.length]).toEqual([0, 1, 1]);
  });

  it('keeps the keyboard inside: focus starts on the safe button, Tab moves between the two, focus returns afterwards', async () => {
    const opener = document.createElement('button');
    document.body.appendChild(opener);
    opener.focus();
    const { fixture, service, buttons } = boot();
    document.body.appendChild(fixture.nativeElement);

    service.ask('Usunąć parafię?', vi.fn());
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.activeElement).toBe(buttons()[0]);
    key('Tab');
    expect(document.activeElement).toBe(buttons()[1]);
    key('Tab', true);
    expect(document.activeElement).toBe(buttons()[0]);

    service.answer(false);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.activeElement).toBe(opener);

    service.ask('Udostępnić notatki?', vi.fn());
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.activeElement).toBe(buttons()[1]);
    service.answer(true);
    opener.remove();
  });

  it('falls back to the browser question when the dialog is not on the page', () => {
    const { fixture, service } = boot();
    fixture.destroy();
    const done = vi.fn();
    const cancelled = vi.fn();
    const native = vi.spyOn(window, 'confirm').mockReturnValueOnce(true).mockReturnValue(false);

    service.ask('Usunąć?', done, { onCancel: cancelled });
    service.ask('Usunąć?', done, { onCancel: cancelled });
    service.ask('Usunąć?', done);

    expect(native).toHaveBeenCalledTimes(3);
    expect([done.mock.calls.length, cancelled.mock.calls.length]).toEqual([1, 1]);
    expect(service.pending()).toBeNull();
  });
});
