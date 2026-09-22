import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BudgetService } from '../budget/budget.service';
import { BudgetEntry, CreateBudgetEntryValue } from '../budget/budget-entry.model';

@Component({
  selector: 'app-budget-dok',
  standalone: true,
  imports: [FormsModule],
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

  constructor(private readonly budgetService: BudgetService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.budgetService.listEntries('DOK').subscribe(entries => this.entries.set(entries));
  }

  openAddForm(): void {
    this.newEntry = { fund: 'DOK', entryDate: '', description: '', category: '', type: 'Expense', amount: 0 };
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
