import { Component, OnInit, computed, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { AttachmentsComponent } from '../../shared/attachments/attachments.component';
import { saveBlob } from '../../shared/attachments/attachment-rules';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { BudgetService } from './budget.service';
import { BudgetEntry, BudgetEntryType, BudgetFund, BudgetPeriod, BudgetSummary } from './budget-entry.model';

type PeriodMode = 'all' | 'year' | 'month';

interface EntryForm {
  entryDate: string;
  description: string;
  category: string;
  type: BudgetEntryType;
  amount: number;
}

export const MONTHS = ['styczeń', 'luty', 'marzec', 'kwiecień', 'maj', 'czerwiec', 'lipiec', 'sierpień', 'wrzesień', 'październik', 'listopad', 'grudzień'];

/** Ewidencja jednego funduszu (SKŚP albo DOK): okres, saldo, struktura wydatków, wpisy z poprawianiem, załącznikami i eksportem. */
@Component({
  selector: 'app-budget-ledger',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule, AttachmentsComponent, DecimalPipe],
  templateUrl: './budget-ledger.component.html'
})
export class BudgetLedgerComponent implements OnInit {
  readonly fund = input.required<BudgetFund>();
  readonly heading = input.required<string>();
  readonly subtitle = input('');
  readonly managePermission = input.required<string>();

  readonly entries = signal<BudgetEntry[]>([]);
  /** Zestawienie z serwera (saldo otwarcia) – tylko gdy wybrano rok lub miesiąc. */
  readonly summary = signal<BudgetSummary | null>(null);
  readonly isFormOpen = signal(false);
  readonly editing = signal<BudgetEntry | null>(null);
  readonly attachmentsEntry = signal<BudgetEntry | null>(null);
  readonly exporting = signal(false);
  form: EntryForm = this.emptyForm();

  readonly months = MONTHS;
  readonly years: number[] = (() => {
    const now = new Date().getFullYear();
    return Array.from({ length: 8 }, (_, i) => now + 1 - i);
  })();
  periodMode: PeriodMode = 'all';
  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;

  readonly totalIncome = computed(() =>
    this.entries().filter(e => e.type === 'Income').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly totalExpense = computed(() =>
    this.entries().filter(e => e.type === 'Expense').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly openingBalance = computed(() => this.summary()?.openingBalance ?? 0);
  readonly balance = computed(() => this.openingBalance() + this.totalIncome() - this.totalExpense());
  readonly expenseByCategory = computed(() => {
    const totals = new Map<string, number>();
    for (const entry of this.entries().filter(e => e.type === 'Expense')) {
      totals.set(entry.category, (totals.get(entry.category) ?? 0) + entry.amount);
    }
    return Array.from(totals.entries()).map(([category, amount]) => ({ category, amount }));
  });

  constructor(
    private readonly budgetService: BudgetService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  private emptyForm(): EntryForm {
    return { entryDate: '', description: '', category: '', type: 'Expense', amount: 0 };
  }

  /** Okres wybrany nad listą jako zakres dat. */
  get period(): BudgetPeriod {
    const pad = (n: number) => String(n).padStart(2, '0');
    if (this.periodMode === 'year') return { from: `${this.year}-01-01`, to: `${this.year}-12-31` };
    if (this.periodMode === 'month') {
      const last = new Date(this.year, this.month, 0).getDate();
      return { from: `${this.year}-${pad(this.month)}-01`, to: `${this.year}-${pad(this.month)}-${pad(last)}` };
    }
    return {};
  }

  get periodLabel(): string {
    if (this.periodMode === 'year') return `rok ${this.year}`;
    if (this.periodMode === 'month') return `${MONTHS[this.month - 1]} ${this.year}`;
    return 'cały okres';
  }

  load(): void {
    const period = this.period;
    this.budgetService.listEntries(this.fund(), period).subscribe({
      next: entries => this.entries.set(entries),
      error: () => this.toast.error('Nie udało się wczytać operacji budżetowych.')
    });
    if (this.periodMode === 'all') {
      this.summary.set(null);
      return;
    }
    this.budgetService.summary(this.fund(), period).subscribe({
      next: summary => this.summary.set(summary),
      error: () => this.summary.set(null)
    });
  }

  onPeriodChange(): void {
    this.load();
  }

  openAddForm(): void {
    this.editing.set(null);
    this.form = this.emptyForm();
    this.isFormOpen.set(true);
  }

  openEditForm(entry: BudgetEntry): void {
    this.editing.set(entry);
    this.form = { entryDate: entry.entryDate, description: entry.description, category: entry.category, type: entry.type, amount: entry.amount };
    this.isFormOpen.set(true);
  }

  get canSave(): boolean {
    return !!this.form.entryDate && !!this.form.description.trim() && !!this.form.category.trim() && Number(this.form.amount) > 0;
  }

  save(): void {
    if (!this.canSave) return;
    const editing = this.editing();
    const value = { ...this.form, amount: Number(this.form.amount) };
    const request$ = editing
      ? this.budgetService.update(editing.id, value)
      : this.budgetService.create({ fund: this.fund(), ...value });
    request$.subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success(editing ? 'Zapisano zmiany operacji.' : 'Dodano operację.');
        this.load();
      },
      error: err => this.toast.error(serverMessage(err, editing ? 'Nie udało się zapisać zmian.' : 'Nie udało się dodać operacji.'))
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }

  deleteEntry(entry: BudgetEntry): void {
    if (!confirm(`Usunąć operację „${entry.description}”?`)) return;
    this.budgetService.delete(entry.id).subscribe({
      next: () => {
        this.toast.success('Operacja usunięta.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się usunąć operacji.')
    });
  }

  openAttachments(entry: BudgetEntry): void {
    this.attachmentsEntry.set(entry);
  }

  closeAttachments(): void {
    this.attachmentsEntry.set(null);
  }

  attachmentsUrl(entry: BudgetEntry): string {
    return `${this.budgetService.baseUrl}/${entry.id}/attachments`;
  }

  /** Po dodaniu/usunięciu pliku odświeżamy listę i otwarty wpis. */
  onAttachmentsChanged(): void {
    const open = this.attachmentsEntry();
    this.budgetService.listEntries(this.fund(), this.period).subscribe({
      next: entries => {
        this.entries.set(entries);
        if (open) this.attachmentsEntry.set(entries.find(e => e.id === open.id) ?? null);
      }
    });
  }

  exportExcel(): void {
    this.exporting.set(true);
    const suffix = this.periodMode === 'all' ? '' : `-${this.period.from?.slice(0, this.periodMode === 'year' ? 4 : 7)}`;
    this.budgetService.export(this.fund(), this.period).subscribe({
      next: blob => {
        saveBlob(blob, `budzet-${this.fund().toLowerCase()}${suffix}.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.toast.error('Nie udało się pobrać pliku Excel.');
        this.exporting.set(false);
      }
    });
  }
}
