import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuditLogComponent } from './audit-log.component';
import { AuditLogEntry } from './audit-log-entry.model';
import { api, clickByText, setup, textOf } from '../../testing/test-helpers';

const entries: AuditLogEntry[] = [
  { id: 'a1', timestampUtc: '2026-10-01T10:00:00Z', userId: 'u1', userEmail: 'admin@example.org', action: 'Login', objectDescription: 'Zalogowano', result: 'Allowed' },
  { id: 'a2', timestampUtc: '2026-10-01T11:00:00Z', userId: 'u1', userEmail: 'admin@example.org', action: 'UpdateRolePermissions', objectDescription: 'Biskup: dodano [A, B]; odebrano []', result: 'Allowed' }
];

function boot(items: AuditLogEntry[] = entries) {
  const ctx = setup(AuditLogComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === api('/api/audit-log')).flush(items);
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
  afterEach(() => vi.restoreAllMocks());

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
});
