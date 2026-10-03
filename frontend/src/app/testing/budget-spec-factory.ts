import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ToastService } from '../core/notifications/toast.service';
import { api, clickByText, setInput, setSelect, setup, textOf } from './test-helpers';

interface BudgetScreen {
  name: string;
  component: Type<unknown>;
  fund: 'SKSP' | 'DOK';
  withExpenseStructure: boolean;
}

export function describeBudgetScreen({ name, component, fund, withExpenseStructure }: BudgetScreen): void {
  const url = api('/api/budget');
  const income = { id: '1', fund, entryDate: '2026-09-12', description: 'Dotacja', category: 'Dotacja', type: 'Income', amount: 5000 };
  const expenseA = { id: '2', fund, entryDate: '2026-09-18', description: 'Materiały', category: 'Materiały', type: 'Expense', amount: 780 };
  const expenseB = { id: '3', fund, entryDate: '2026-09-20', description: 'Książki', category: 'Materiały', type: 'Expense', amount: 220 };

  function boot(entries: unknown[] = [income, expenseA, expenseB]) {
    const ctx = setup(component);
    ctx.fixture.detectChanges();
    ctx.http.expectOne(r => r.url === url && r.params.get('fund') === fund).flush(entries);
    ctx.fixture.detectChanges();
    return ctx;
  }

  const toastMessages = () => TestBed.inject(ToastService).toasts().map(t => t.message);

  describe(name, () => {
    afterEach(() => vi.restoreAllMocks());

    it('computes income, expense and balance totals from the fetched entries', () => {
      const { el } = boot();

      const text = textOf(el);
      expect(text).toContain('5000');
      expect(text).toContain('1000');
      expect(text).toContain('4000');
    });

    it('lists entries with signs depending on their type', () => {
      const { el } = boot();

      const text = textOf(el);
      expect(text).toContain('+ 5000 zł');
      expect(text).toContain('- 780 zł');
    });

    if (withExpenseStructure) {
      it('groups expenses by category', () => {
        const { fixture } = boot();
        const grouped = (fixture.componentInstance as unknown as { expenseByCategory: () => { category: string; amount: number }[] }).expenseByCategory();

        expect(grouped).toEqual([{ category: 'Materiały', amount: 1000 }]);
      });
    }

    it('shows a toast when the entries cannot be loaded', () => {
      const { fixture, http } = setup(component);
      fixture.detectChanges();

      http.expectOne(r => r.url === url).flush('x', { status: 500, statusText: 'Server Error' });

      expect(toastMessages()).toContain('Nie udało się wczytać operacji budżetowych.');
    });

    describe('adding an entry', () => {
      async function openForm() {
        const ctx = boot();
        clickByText(ctx.el, 'Dodaj operację');
        ctx.fixture.detectChanges();
        await ctx.fixture.whenStable();
        return ctx;
      }

      it(`posts the typed entry for the ${fund} fund, confirms and reloads`, async () => {
        const ctx = await openForm();

        setInput(ctx.el, 'input[name="entryDate"]', '2026-10-01');
        setSelect(ctx.el, 'select[name="type"]', 'Income');
        setInput(ctx.el, 'input[name="description"]', 'Ofiara');
        setInput(ctx.el, 'input[name="category"]', 'Dotacja');
        setInput(ctx.el, 'input[name="amount"]', '150');
        clickByText(ctx.el, 'Zapisz', '.modal-foot button');

        const req = ctx.http.expectOne(r => r.method === 'POST' && r.url === url);
        expect(req.request.body).toEqual({ fund, entryDate: '2026-10-01', description: 'Ofiara', category: 'Dotacja', type: 'Income', amount: 150 });
        req.flush(income);
        ctx.fixture.detectChanges();

        expect(toastMessages()).toContain('Dodano operację.');
        expect(ctx.el.querySelector('.modal')).toBeNull();
        ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([income]);
      });

      it('shows a toast when adding fails and closes the form on cancel', async () => {
        const ctx = await openForm();

        clickByText(ctx.el, 'Zapisz', '.modal-foot button');
        ctx.http.expectOne(r => r.method === 'POST').flush('x', { status: 400, statusText: 'Bad Request' });
        expect(toastMessages()).toContain('Nie udało się dodać operacji.');

        clickByText(ctx.el, 'Anuluj', '.modal-foot button');
        ctx.fixture.detectChanges();
        expect(ctx.el.querySelector('.modal')).toBeNull();
      });
    });

    describe('deleting an entry', () => {
      it('deletes after confirmation and reloads', () => {
        const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
        const ctx = boot();

        clickByText(ctx.el, 'Usuń');
        expect(confirmSpy).toHaveBeenCalledWith('Usunąć operację „Dotacja”?');
        ctx.http.expectOne(r => r.method === 'DELETE' && r.url === `${url}/1`).flush(null);

        expect(toastMessages()).toContain('Operacja usunięta.');
        ctx.http.expectOne(r => r.method === 'GET' && r.url === url).flush([]);
      });

      it('does nothing when the confirmation is cancelled', () => {
        vi.spyOn(window, 'confirm').mockReturnValue(false);
        const ctx = boot();

        clickByText(ctx.el, 'Usuń');

        ctx.http.expectNone(r => r.method === 'DELETE');
      });

      it('shows a toast when deleting fails', () => {
        vi.spyOn(window, 'confirm').mockReturnValue(true);
        const ctx = boot();

        clickByText(ctx.el, 'Usuń');
        ctx.http.expectOne(r => r.method === 'DELETE').flush('x', { status: 500, statusText: 'Server Error' });

        expect(toastMessages()).toContain('Nie udało się usunąć operacji.');
      });
    });
  });
}
