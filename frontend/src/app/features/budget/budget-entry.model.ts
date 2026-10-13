import { Attachment } from '../../shared/attachments/attachment.model';

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
  /** Faktury, paragony itp. */
  attachments?: Attachment[];
}

export interface CreateBudgetEntryValue {
  fund: BudgetFund;
  entryDate: string;
  description: string;
  category: string;
  type: BudgetEntryType;
  amount: number;
}

/** Okres: od–do (RRRR-MM-DD, obie daty włącznie); puste = cały okres. */
export interface BudgetPeriod {
  from?: string;
  to?: string;
}

export interface BudgetSummary {
  openingBalance: number;
  income: number;
  expense: number;
  closingBalance: number;
}
