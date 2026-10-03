import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ToastContainerComponent } from './toast-container.component';
import { ToastService } from './toast.service';

describe('ToastContainerComponent', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ imports: [ToastContainerComponent] });
  });

  afterEach(() => vi.useRealTimers());

  function render() {
    const fixture = TestBed.createComponent(ToastContainerComponent);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, toasts: TestBed.inject(ToastService) };
  }

  it('shows success and error messages, errors with the error style', () => {
    const { fixture, el, toasts } = render();

    toasts.success('Zapisano.');
    toasts.error('Błąd zapisu.');
    fixture.detectChanges();

    const items = Array.from(el.querySelectorAll('.toast'));
    expect(items.map(i => i.textContent!.trim())).toEqual(['Zapisano.', 'Błąd zapisu.']);
    expect(items[0].classList.contains('error')).toBe(false);
    expect(items[1].classList.contains('error')).toBe(true);
  });

  it('dismisses a message when it is clicked', () => {
    const { fixture, el, toasts } = render();
    toasts.success('Zapisano.');
    fixture.detectChanges();

    (el.querySelector('.toast') as HTMLElement).click();
    fixture.detectChanges();

    expect(el.querySelectorAll('.toast').length).toBe(0);
  });

  it('removes a message by itself after a few seconds', () => {
    const { fixture, el, toasts } = render();
    toasts.error('Chwilowy błąd.');
    fixture.detectChanges();
    expect(el.querySelectorAll('.toast').length).toBe(1);

    vi.advanceTimersByTime(4001);
    fixture.detectChanges();

    expect(el.querySelectorAll('.toast').length).toBe(0);
  });
});
