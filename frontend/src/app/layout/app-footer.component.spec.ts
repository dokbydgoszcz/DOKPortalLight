import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import packageJson from '../../../package.json';
import { AppFooterComponent } from './app-footer.component';
import { APP_VERSION } from '../core/app-info';

function render() {
  const fixture = TestBed.createComponent(AppFooterComponent);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return { fixture, el, text: (el.textContent ?? '').replace(/\s+/g, ' ').trim() };
}

describe('AppFooterComponent', () => {
  afterEach(() => vi.useRealTimers());

  it('shows the copyright with the diocese and the organisation', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2027-03-05T10:00:00Z'));

    const { text } = render();

    expect(text).toContain('© 2027 Diecezja Bydgoska');
    expect(text).toContain('Diecezjalny Ośrodek Katechumenalny');
  });

  it('shows the application name without "Light" and the version', () => {
    const { text } = render();

    expect(text).toContain(`DOK Portal · wersja ${APP_VERSION}`);
    expect(text).not.toContain('Light');
  });

  it('takes the version from package.json, so there is one place to bump it', () => {
    expect(APP_VERSION).toBe(packageJson.version);
    expect(APP_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });

  it('is a footer landmark', () => {
    const { el } = render();

    expect(el.querySelector('footer.app-footer')).not.toBeNull();
  });
});
