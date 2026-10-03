import { HasPermissionDirective } from '../../shared/permissions/has-permission.directive';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BudgetService } from '../budget/budget.service';
import { BudgetEntry, CreateBudgetEntryValue } from '../budget/budget-entry.model';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-budget-dok',
  standalone: true,
  imports: [HasPermissionDirective, FormsModule],
  templateUrl: './budget-dok.component.html',
  styleUrl: './budget-dok.component.scss'
})
export class BudgetDokComponent implements OnInit {
  readonly entries = signal<BudgetEntry[]>([]);
  readonly isFormOpen = signal(false);
  newEntry: CreateBudgetEntryValue = {
    fund: 'DOK', entryDate: '', description: '', category: '', type: 'Expense', amount: 0
  };

  readonly totalIncome = computed(() =>
    this.entries().filter(e => e.type === 'Income').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly totalExpense = computed(() =>
    this.entries().filter(e => e.type === 'Expense').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly balance = computed(() => this.totalIncome() - this.totalExpense());

  constructor(
    private readonly budgetService: BudgetService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.budgetService.listEntries('DOK').subscribe({
      next: entries => this.entries.set(entries),
      error: () => this.toast.error('Nie udało się wczytać operacji budżetowych.')
    });
  }

  openAddForm(): void {
    this.newEntry = { fund: 'DOK', entryDate: '', description: '', category: '', type: 'Expense', amount: 0 };
    this.isFormOpen.set(true);
  }

  createEntry(): void {
    this.budgetService.create(this.newEntry).subscribe({
      next: () => {
        this.isFormOpen.set(false);
        this.toast.success('Dodano operację.');
        this.load();
      },
      error: () => this.toast.error('Nie udało się dodać operacji.')
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
}
