export type BudgetFund = 'SKSP' | 'DOK';
export type BudgetEntryType = 'Income' | 'Expense';

export interface BudgetEntry {
  id: string;
  fund: BudgetFund;
  entryDate: string;
  description: string;
  category: string;
  type: BudgetEntryType;
  amount: number;
}

export interface CreateBudgetEntryValue {
  fund: BudgetFund;
  entryDate: string;
  description: string;
  category: string;
  type: BudgetEntryType;
  amount: number;
}
