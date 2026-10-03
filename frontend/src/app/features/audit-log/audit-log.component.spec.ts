import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ToastService } from '../../core/notifications/toast.service';
import { AuditLogComponent } from './audit-log.component';
import { AuditLogEntry } from './audit-log-entry.model';
import { api, clickByText, setInput, setSelect, setup, textOf } from '../../testing/test-helpers';

const entries: AuditLogEntry[] = [
  { id: 'a1', timestampUtc: '2026-10-01T10:00:00Z', userId: 'u1', userEmail: 'admin@example.org', action: 'Login', objectDescription: 'Zalogowano', result: 'Allowed' },
  { id: 'a2', timestampUtc: '2026-10-01T11:00:00Z', userId: 'u1', userEmail: 'admin@example.org', action: 'UpdateRolePermissions', objectDescription: 'Biskup: dodano [A, B]; odebrano []', result: 'Allowed' }
];

const listUrl = api('/api/audit-log');

function boot(items: AuditLogEntry[] = entries, actions: string[] = ['Login', 'UpdateRolePermissions']) {
  const ctx = setup(AuditLogComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === listUrl).flush(items);
  ctx.http.expectOne(r => r.url === `${listUrl}/actions`).flush(actions);
  ctx.fixture.detectChanges();
  return ctx;
}

async function exportedCsv(el: HTMLElement): Promise<{ text: string; fileName: string }> {
  let blob: Blob | undefined;
  URL.createObjectURL = vi.fn((b: Blob) => {
    blob = b;
    return 'blob:csv';
  });
  URL.revokeObjectURL = vi.fn();
  let fileName = '';
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    fileName = this.download;
  });
  clickByText(el, 'Eksportuj CSV');
  return { text: await blob!.text(), fileName };
}

describe('AuditLogComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('renders the audit entries returned from the API', () => {
    const { el } = boot();

    expect(textOf(el)).toContain('admin@example.org');
    expect(textOf(el)).toContain('UpdateRolePermissions');
  });

  it('exports the entries as a CSV file with a header row', async () => {
    const { el } = boot([entries[0]]);

    const { text, fileName } = await exportedCsv(el);

    expect(fileName).toBe('audit-log.csv');
    expect(text.split('\n')).toEqual([
      'Data,Uzytkownik,Akcja,Obiekt,Wynik',
      '2026-10-01T10:00:00Z,admin@example.org,Login,Zalogowano,Allowed'
    ]);
  });

  it('quotes fields that contain commas so that columns do not shift', async () => {
    const { el } = boot();

    const { text } = await exportedCsv(el);

    const lines = text.split('\n');
    expect(lines[2]).toBe('2026-10-01T11:00:00Z,admin@example.org,UpdateRolePermissions,"Biskup: dodano [A, B]; odebrano []",Allowed');
  });

  it('escapes double quotes inside fields', async () => {
    const { el } = boot([{ ...entries[0], objectDescription: 'Rola „X” "test"' }]);

    const { text } = await exportedCsv(el);

    expect(text.split('\n')[1]).toBe('2026-10-01T10:00:00Z,admin@example.org,Login,"Rola „X” ""test""",Allowed');
  });

  it('loads the newest 500 entries without any filter at first', () => {
    const { fixture, http } = setup(AuditLogComponent);
    fixture.detectChanges();

    const req = http.expectOne(r => r.url === listUrl);
    expect(req.request.params.keys().sort()).toEqual(['take']);
    expect(req.request.params.get('take')).toBe('500');
    req.flush([]);
    http.expectOne(r => r.url === `${listUrl}/actions`).flush([]);
  });

  it('shows a readable timestamp and how many entries are displayed', () => {
    const { el } = boot();

    expect(textOf(el)).toMatch(/01\.10\.2026 \d{2}:\d{2}:\d{2}/);
    expect(textOf(el)).not.toContain('2026-10-01T');
    expect(textOf(el)).toContain('Wyświetlono 2 wpisy');
  });

  it('tells you to narrow the filters when the limit is reached', () => {
    const many = Array.from({ length: 500 }, (_, i) => ({ ...entries[0], id: `m${i}` }));
    const { el } = boot(many);

    expect(textOf(el)).toContain('najnowsze 500');
  });

  it('shows a toast when the entries cannot be loaded', () => {
    const { fixture, http } = setup(AuditLogComponent);
    fixture.detectChanges();

    http.expectOne(r => r.url === listUrl).flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => r.url === `${listUrl}/actions`).flush([]);

    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toContain('Nie udało się wczytać dziennika audytu.');
  });

  describe('filters', () => {
    const lastList = (http: ReturnType<typeof boot>['http']) => http.expectOne(r => r.url === listUrl);

    it('searches the server after a short pause while typing, and ignores blank text', () => {
      vi.useFakeTimers();
      const { fixture, http, el } = boot();

      setInput(el, 'input[name="auditSearch"]', 'anna');
      vi.advanceTimersByTime(100);
      http.expectNone(r => r.url === listUrl);
      vi.advanceTimersByTime(300);
      const req = lastList(http);
      expect(req.request.params.get('search')).toBe('anna');
      req.flush([entries[0]]);
      fixture.detectChanges();
      expect(textOf(el)).toContain('Wyświetlono 1 wpis');

      setInput(el, 'input[name="auditSearch"]', '   ');
      vi.advanceTimersByTime(300);
      expect(lastList(http).request.params.has('search')).toBe(false);
      vi.useRealTimers();
    });

    it('offers the action names known to the server and filters by the chosen one', () => {
      const { http, el } = boot();
      const options = Array.from(el.querySelectorAll('select[name="auditAction"] option')).map(o => (o as HTMLOptionElement).value);
      expect(options).toEqual(['', 'Login', 'UpdateRolePermissions']);

      setSelect(el, 'select[name="auditAction"]', 'Login');

      expect(lastList(http).request.params.get('action')).toBe('Login');
    });

    it('filters by result', () => {
      const { http, el } = boot();

      setSelect(el, 'select[name="auditResult"]', 'Blocked');

      expect(lastList(http).request.params.get('result')).toBe('Blocked');
    });

    it('filters by date range', () => {
      const { http, el } = boot();

      setInput(el, 'input[name="auditFrom"]', '2026-10-01');
      expect(lastList(http).request.params.get('from')).toBe('2026-10-01');
      setInput(el, 'input[name="auditTo"]', '2026-10-03');

      const req = lastList(http);
      expect(req.request.params.get('from')).toBe('2026-10-01');
      expect(req.request.params.get('to')).toBe('2026-10-03');
    });

    it('clears all filters and reloads the unfiltered list', async () => {
      const { fixture, http, el } = boot();
      setSelect(el, 'select[name="auditResult"]', 'Blocked');
      lastList(http).flush([]);
      fixture.detectChanges();

      clickByText(el, 'Wyczyść filtry');

      const req = lastList(http);
      expect(req.request.params.keys().sort()).toEqual(['take']);
      req.flush(entries);
      fixture.detectChanges();
      await fixture.whenStable();
      expect((el.querySelector('select[name="auditResult"]') as HTMLSelectElement).value).toBe('');
    });

    it('works without the action list when it cannot be loaded', () => {
      const { fixture, http, el } = setup(AuditLogComponent);
      fixture.detectChanges();
      http.expectOne(r => r.url === listUrl).flush([]);
      http.expectOne(r => r.url === `${listUrl}/actions`).flush('x', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(el.querySelectorAll('select[name="auditAction"] option').length).toBe(1);
    });
  });
});
