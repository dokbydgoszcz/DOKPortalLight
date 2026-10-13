import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi, afterEach } from 'vitest';
import { BudgetComponent } from './budget.component';
import { BudgetLedgerComponent } from './budget-ledger.component';
import { By } from '@angular/platform-browser';
import { ToastService } from '../../core/notifications/toast.service';
import { api, setup, textOf } from '../../testing/test-helpers';

const url = api('/api/budget');
const entry = { id: '1', fund: 'SKSP', entryDate: '2026-09-12', description: 'Faktura', category: 'Materiały', type: 'Expense', amount: 100, attachments: [] };

function boot() {
  const ctx = setup(BudgetComponent);
  ctx.fixture.detectChanges();
  ctx.http.expectOne(r => r.url === url).flush([entry]);
  ctx.fixture.detectChanges();
  const ledger = ctx.fixture.debugElement.query(By.directive(BudgetLedgerComponent)).componentInstance as BudgetLedgerComponent;
  return { ...ctx, ledger };
}

const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('BudgetLedgerComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('turns a year and a month into date ranges and labels', () => {
    const { ledger } = boot();
    ledger.year = 2026;
    ledger.periodMode = 'year';
    expect(ledger.period).toEqual({ from: '2026-01-01', to: '2026-12-31' });
    expect(ledger.periodLabel).toBe('rok 2026');
    ledger.periodMode = 'month';
    ledger.month = 2;
    expect(ledger.period).toEqual({ from: '2026-02-01', to: '2026-02-28' });
    expect(ledger.periodLabel).toBe('luty 2026');
    ledger.periodMode = 'all';
    expect(ledger.periodLabel).toBe('cały okres');
  });

  it('drops the opening balance when the summary fails, and names the export after the period', () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const names: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { names.push(this.download); });
    const { ledger, http } = boot();
    ledger.year = 2025;
    ledger.periodMode = 'year';
    ledger.onPeriodChange();
    http.expectOne(r => r.url === url && r.params.get('from') === '2025-01-01').flush([]);
    http.expectOne(`${url}/summary?fund=SKSP&from=2025-01-01&to=2025-12-31`).flush('x', { status: 500, statusText: 'Error' });
    expect(ledger.summary()).toBeNull();

    ledger.exportExcel();
    http.expectOne(r => r.url === `${url}/export`).flush(new Blob(['x']));
    expect(names).toEqual(['budzet-sksp-2025.xlsx']);
    expect(ledger.exporting()).toBe(false);

    ledger.exportExcel();
    http.expectOne(r => r.url === `${url}/export`).flush(new Blob(['x']), { status: 500, statusText: 'Error' });
    expect(toastMessages()).toContain('Nie udało się pobrać pliku Excel.');
  });

  it('shows the server reason when a correction is refused, and closes on cancel', () => {
    const { ledger, http } = boot();
    ledger.openEditForm(entry as never);
    ledger.save();
    http.expectOne(r => r.method === 'PUT').flush({ title: 'Kwota musi być większa od zera.' }, { status: 400, statusText: 'Bad Request' });
    expect(toastMessages()).toContain('Kwota musi być większa od zera.');

    ledger.cancel();
    expect(ledger.isFormOpen()).toBe(false);
  });

  it('refreshes the open attachments window after a file is added, and closes it', () => {
    const { ledger, http, el, fixture } = boot();
    ledger.openAttachments(entry as never);
    fixture.detectChanges();
    expect(textOf(el)).toContain('Załączniki — Faktura');

    ledger.onAttachmentsChanged();
    http.expectOne(r => r.url === url).flush([{ ...entry, attachments: [{ id: 'a1', fileName: 'f.pdf', contentType: 'application/pdf', sizeBytes: 5, uploadedAtUtc: '2026-10-01T10:00:00Z' }] }]);
    expect(ledger.attachmentsEntry()?.attachments?.length).toBe(1);
    expect(ledger.attachmentsUrl(entry as never)).toBe(`${url}/1/attachments`);

    ledger.closeAttachments();
    expect(ledger.attachmentsEntry()).toBeNull();
  });
  it('switches the period with the selects and closes windows with ×', async () => {
    const { el, fixture, http, ledger } = boot();
    await fixture.whenStable();
    const select = (name: string, value: string) => {
      const s = el.querySelector(`select[name="${name}"]`) as HTMLSelectElement;
      s.value = value;
      s.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    select('periodMode', 'month');
    http.match(r => r.url === url || r.url === `${url}/summary`).forEach(r => r.flush(r.request.url.endsWith('summary') ? { openingBalance: 0, income: 0, expense: 0, closingBalance: 0 } : []));
    await fixture.whenStable();
    fixture.detectChanges();
    const monthSelect = el.querySelector('select[name="periodMonth"]') as HTMLSelectElement;
    monthSelect.value = monthSelect.options[0].value;
    monthSelect.dispatchEvent(new Event('change'));
    const yearSelect = el.querySelector('select[name="periodYear"]') as HTMLSelectElement;
    yearSelect.value = yearSelect.options[1].value;
    yearSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(ledger.month).toBe(1);
    expect(ledger.year).toBe(new Date().getFullYear());
    http.match(() => true).forEach(r => r.flush(r.request.url.endsWith('summary') ? { openingBalance: 0, income: 0, expense: 0, closingBalance: 0 } : []));

    ledger.openAddForm();
    fixture.detectChanges();
    (el.querySelector('.modal .close') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(ledger.isFormOpen()).toBe(false);

    ledger.openAttachments(entry as never);
    fixture.detectChanges();
    (el.querySelector('.modal .close') as HTMLButtonElement).click();
    expect(ledger.attachmentsEntry()).toBeNull();
    ledger.openAttachments(entry as never);
    fixture.detectChanges();
    (Array.from(el.querySelectorAll('.modal-foot button')).find(b => b.textContent?.includes('Zamknij')) as HTMLButtonElement).click();
    expect(ledger.attachmentsEntry()).toBeNull();
  });
});
