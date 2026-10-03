import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AttachmentsComponent } from './attachments.component';
import { Attachment } from './attachment.model';
import { ToastService } from '../../core/notifications/toast.service';
import { clickByText, setup, textOf } from '../../testing/test-helpers';

const base = 'http://api.test/api/supervisions/s1/attachments';
const first: Attachment = { id: 'a1', fileName: 'protokol.pdf', contentType: 'application/pdf', sizeBytes: 2048, uploadedAtUtc: '2026-10-03T10:30:00Z' };
const second: Attachment = { id: 'a2', fileName: 'zdjecie.jpg', contentType: 'image/jpeg', sizeBytes: 3 * 1024 * 1024, uploadedAtUtc: '2026-10-02T09:00:00Z' };

@Component({
  standalone: true,
  imports: [AttachmentsComponent],
  template: `<app-attachments [attachments]="items()" [baseUrl]="url" managePermission="Supervisions.Manage" [showEmpty]="showEmpty" [showHint]="showHint" (changed)="changes = changes + 1" />`
})
class HostComponent {
  readonly items = signal<Attachment[]>([first, second]);
  readonly url = base;
  showEmpty = false;
  showHint = true;
  changes = 0;
}

function boot(options: { items?: Attachment[]; granted?: string[]; showEmpty?: boolean; showHint?: boolean } = {}) {
  const ctx = setup(HostComponent, { granted: options.granted });
  ctx.fixture.componentInstance.items.set(options.items ?? [first, second]);
  ctx.fixture.componentInstance.showEmpty = options.showEmpty ?? false;
  ctx.fixture.componentInstance.showHint = options.showHint ?? true;
  ctx.fixture.detectChanges();
  return ctx;
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);
const fileInput = (el: HTMLElement) => el.querySelector('input[type="file"]') as HTMLInputElement;

function choose(el: HTMLElement, files: File[]) {
  const input = fileInput(el);
  Object.defineProperty(input, 'files', { value: files, configurable: true });
  input.dispatchEvent(new Event('change'));
}

describe('AttachmentsComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('lists each file with its size and date', () => {
    const { el } = boot();

    const text = textOf(el);
    expect(text).toContain('protokol.pdf');
    expect(text).toContain('2.0 KB');
    expect(text).toContain('03.10.2026');
    expect(text).toContain('zdjecie.jpg');
    expect(text).toContain('3.0 MB');
  });

  it('shows the rules and a multi-file picker restricted to the allowed types', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('PDF, JPG/JPEG, PNG, DOCX, DOC, TXT');
    expect(textOf(el)).toContain('20 MB');
    expect(fileInput(el).multiple).toBe(true);
    expect(fileInput(el).accept).toBe('.pdf,.jpg,.jpeg,.png,.docx,.doc,.txt');
  });

  it('can hide the rules hint while keeping the picker', () => {
    const { el } = boot({ showHint: false });

    expect(textOf(el)).not.toContain('20 MB');
    expect(fileInput(el)).not.toBeNull();
  });

  it('says there are no files when asked to', () => {
    expect(textOf(boot({ items: [], showEmpty: true }).el)).toContain('Brak załączników.');
  });

  it('stays quiet about missing files by default', () => {
    expect(textOf(boot({ items: [] }).el)).not.toContain('Brak załączników.');
  });

  it('hides adding and deleting from users without the manage permission, but keeps downloading', () => {
    const { el } = boot({ granted: ['Supervisions.View'] });

    expect(fileInput(el)).toBeNull();
    expect(textOf(el)).not.toContain('Usuń');
    expect(textOf(el)).not.toContain('Dodaj plik');
    expect(textOf(el)).toContain('Pobierz');
  });

  describe('downloading', () => {
    it('saves the file under its name', () => {
      const ctx = boot();
      URL.createObjectURL = vi.fn(() => 'blob:test');
      URL.revokeObjectURL = vi.fn();
      const anchorClick = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

      clickByText(ctx.el, 'Pobierz');
      ctx.http.expectOne(r => r.method === 'GET' && r.url === `${base}/a1/download`).flush(new Blob(['x']));

      expect(anchorClick).toHaveBeenCalledTimes(1);
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    });

    it('shows a toast when the download fails', () => {
      const ctx = boot();

      clickByText(ctx.el, 'Pobierz');
      ctx.http.expectOne(r => r.url === `${base}/a1/download`).error(new ProgressEvent('error'), { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się pobrać pliku.');
    });
  });

  describe('deleting', () => {
    it('deletes after confirmation, informs the user and asks the parent to reload', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${base}/a1`).flush(null, { status: 204, statusText: 'No Content' });

      expect(toastMessages()).toContain('Załącznik usunięty.');
      expect(ctx.fixture.componentInstance.changes).toBe(1);
    });

    it('does nothing when the confirmation is declined', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(false);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');

      ctx.http.expectNone(r => r.method === 'DELETE');
    });

    it('shows a toast when deleting fails and does not ask for a reload', () => {
      vi.spyOn(window, 'confirm').mockReturnValue(true);
      const ctx = boot();

      clickByText(ctx.el, 'Usuń');
      ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się usunąć załącznika.');
      expect(ctx.fixture.componentInstance.changes).toBe(0);
    });
  });

  describe('adding files', () => {
    it('uploads every chosen file and asks the parent to reload once', () => {
      const ctx = boot();

      choose(ctx.el, [new File(['a'], 'a.pdf'), new File(['b'], 'b.png')]);
      ctx.http.expectOne(r => r.method === 'POST' && r.url === base).flush(first);
      ctx.http.expectOne(r => r.method === 'POST' && r.url === base).flush(second);

      expect(toastMessages()).toContain('Dodano pliki: 2.');
      expect(ctx.fixture.componentInstance.changes).toBe(1);
    });

    it('confirms a single file in the singular', () => {
      const ctx = boot();

      choose(ctx.el, [new File(['a'], 'a.pdf')]);
      ctx.http.expectOne(r => r.method === 'POST').flush(first);

      expect(toastMessages()).toContain('Dodano plik.');
    });

    it('does nothing when no file was chosen', () => {
      const ctx = boot();

      choose(ctx.el, []);

      ctx.http.expectNone(r => r.method === 'POST');
      expect(ctx.fixture.componentInstance.changes).toBe(0);
    });

    it('reports a forbidden file without sending it, and still uploads the good one', () => {
      const ctx = boot();

      choose(ctx.el, [new File(['MZ'], 'virus.exe'), new File(['a'], 'a.pdf')]);
      ctx.http.expectOne(r => r.method === 'POST').flush(first);

      expect(toastMessages().some(m => m.startsWith('virus.exe: Niedozwolony typ pliku'))).toBe(true);
      expect(ctx.fixture.componentInstance.changes).toBe(1);
    });

    it('shows the server message and does not reload when nothing was uploaded', () => {
      const ctx = boot();

      choose(ctx.el, [new File(['a'], 'a.pdf')]);
      ctx.http.expectOne(r => r.method === 'POST').flush({ title: 'Plik jest za duży – maksymalny rozmiar to 20 MB.' }, { status: 400, statusText: 'Bad Request' });

      expect(toastMessages()).toContain('a.pdf: Plik jest za duży – maksymalny rozmiar to 20 MB.');
      expect(ctx.fixture.componentInstance.changes).toBe(0);
    });
  });
});
