import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LetterheadSettingsComponent } from './letterhead-settings.component';
import { ToastService } from '../../core/notifications/toast.service';
import { api, setup, textOf } from '../../testing/test-helpers';

const url = api('/api/document-templates/letterhead');
const head = { headerText: 'DOK\nul. Grodzka 18', footerText: 'tel. 52', place: 'Bydgoszcz', hasLogo: true, updatedAtUtc: null };
const toasts = () => TestBed.inject(ToastService).toasts().map(m => m.message);
const button = (el: HTMLElement, text: string) => Array.from(el.querySelectorAll('button')).find(b => b.textContent!.trim() === text) as HTMLButtonElement;

function boot(value = head) {
  vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:logo');
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  const ctx = setup(LetterheadSettingsComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(url).flush(value);
  if (value.hasLogo) ctx.http.expectOne(`${url}/logo`).flush(new Blob(['png']));
  ctx.fixture.detectChanges();
  return ctx;
}

const pick = (el: HTMLElement, file: File) => {
  const input = el.querySelector('input[type="file"]') as HTMLInputElement;
  Object.defineProperty(input, 'files', { value: [file], configurable: true });
  input.dispatchEvent(new Event('change'));
};

describe('LetterheadSettingsComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('shows and saves the header, footer and place', async () => {
    const ctx = boot();
    await ctx.fixture.whenStable();
    ctx.fixture.detectChanges();
    expect((ctx.el.querySelector('#lh-header') as HTMLTextAreaElement).value).toBe('DOK\nul. Grodzka 18');
    expect(ctx.el.querySelector('.logo-box img')!.getAttribute('src')).toBe('blob:logo');
    for (const [id, value] of [['#lh-header', 'DOK\nul. Grodzka 18'], ['#lh-footer', 'tel. 52']]) {
      const field = ctx.el.querySelector(id) as HTMLInputElement;
      field.value = value;
      field.dispatchEvent(new Event('input'));
    }
    const place = ctx.el.querySelector('#lh-place') as HTMLInputElement;
    place.value = 'Nakło';
    place.dispatchEvent(new Event('input'));
    button(ctx.el, 'Zapisz').click();
    const put = ctx.http.expectOne(r => r.url === url && r.method === 'PUT');
    expect(put.request.body).toEqual({ headerText: 'DOK\nul. Grodzka 18', footerText: 'tel. 52', place: 'Nakło' });
    put.flush(head);
    expect(toasts()).toContain('Zapisano nagłówek i stopkę.');
    button(ctx.el, 'Zapisz').click();
    ctx.http.expectOne(r => r.url === url && r.method === 'PUT').flush({ title: 'Tekst może mieć najwyżej 100 znaków.' }, { status: 400, statusText: 'Bad Request' });
    expect(toasts()).toContain('Tekst może mieć najwyżej 100 znaków.');
  });

  it('uploads, refuses and removes the logo', () => {
    const ctx = boot({ ...head, hasLogo: false, headerText: null as never, footerText: null as never, place: null as never });
    expect(textOf(ctx.el)).toContain('bez logo');
    pick(ctx.el, new File(['x'], 'logo.gif', { type: 'image/gif' }));
    expect(toasts()).toContain('Logo musi być obrazem PNG albo JPG.');
    pick(ctx.el, new File(['x'], 'logo.png', { type: 'image/png' }));
    ctx.http.expectOne(`${url}/logo`).flush(head);
    ctx.fixture.detectChanges();
    expect(toasts()).toContain('Zapisano logo.');
    pick(ctx.el, new File(['x'], 'logo.jpg', { type: 'image/jpeg' }));
    ctx.http.expectOne(`${url}/logo`).flush({ title: 'Logo jest za duże – najwyżej 2 MB.' }, { status: 400, statusText: 'Bad Request' });
    expect(toasts()).toContain('Logo jest za duże – najwyżej 2 MB.');

    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValue(true);
    button(ctx.el, 'Usuń logo').click();
    ctx.http.expectNone(r => r.method === 'DELETE');
    button(ctx.el, 'Usuń logo').click();
    ctx.http.expectOne(r => r.method === 'DELETE').flush({ ...head, hasLogo: false });
    ctx.fixture.detectChanges();
    expect(textOf(ctx.el)).toContain('bez logo');
    const input = ctx.el.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [], configurable: true });
    input.dispatchEvent(new Event('change'));
    ctx.fixture.componentInstance.removeLogo();
    ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Error' });
    expect(toasts()).toContain('Nie udało się usunąć logo.');
  });

  it('previews a sample letter, and reports load and logo problems', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const ctx = setup(LetterheadSettingsComponent);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(url).flush('x', { status: 500, statusText: 'Error' });
    expect(toasts()).toContain('Nie udało się wczytać nagłówka pism.');
    button(ctx.el, 'Podgląd').click();
    const preview = ctx.http.expectOne(api('/api/document-templates/preview'));
    expect(preview.request.body.useLetterhead).toBe(true);
    preview.flush(new Blob(['%PDF']));
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-pdf-preview')).not.toBeNull();
    button(ctx.el, 'Zamknij').click();
    ctx.fixture.detectChanges();
    expect(ctx.el.querySelector('app-pdf-preview')).toBeNull();
    button(ctx.el, 'Podgląd').click();
    ctx.http.expectOne(api('/api/document-templates/preview')).flush(new Blob(['x']), { status: 500, statusText: 'Error' });
    await new Promise(r => setTimeout(r, 0));
    expect(toasts()).toContain('Nie udało się przygotować podglądu.');

    const second = ctx.fixture.componentInstance;
    (second as unknown as { apply: (h: typeof head) => void }).apply(head);
    ctx.http.expectOne(`${url}/logo`).flush(new Blob(['x']), { status: 404, statusText: 'Not Found' });
    expect(second.logoUrl()).toBeNull();
  });
});
