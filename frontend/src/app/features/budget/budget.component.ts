import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BudgetService } from './budget.service';
import { BudgetEntry, CreateBudgetEntryValue } from './budget-entry.model';

@Component({
  selector: 'app-budget',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './budget.component.html',
  styleUrl: './budget.component.scss'
})
export class BudgetComponent implements OnInit {
  readonly entries = signal<BudgetEntry[]>([]);
  readonly isFormOpen = signal(false);
  newEntry: CreateBudgetEntryValue = {
    fund: 'SKSP', entryDate: '', description: '', category: '', type: 'Expense', amount: 0
  };

  readonly totalIncome = computed(() =>
    this.entries().filter(e => e.type === 'Income').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly totalExpense = computed(() =>
    this.entries().filter(e => e.type === 'Expense').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly balance = computed(() => this.totalIncome() - this.totalExpense());
  readonly expenseByCategory = computed(() => {
    const totals = new Map<string, number>();
    for (const entry of this.entries().filter(e => e.type === 'Expense')) {
      totals.set(entry.category, (totals.get(entry.category) ?? 0) + entry.amount);
    }
    return Array.from(totals.entries()).map(([category, amount]) => ({ category, amount }));
  });

  constructor(private readonly budgetService: BudgetService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.budgetService.listEntries('SKSP').subscribe(entries => this.entries.set(entries));
  }

  openAddForm(): void {
    this.newEntry = { fund: 'SKSP', entryDate: '', description: '', category: '', type: 'Expense', amount: 0 };
    this.isFormOpen.set(true);
  }

  createEntry(): void {
    this.budgetService.create(this.newEntry).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
