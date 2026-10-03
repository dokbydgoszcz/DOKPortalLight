import { BudgetComponent } from './budget.component';
import { describeBudgetScreen } from '../../testing/budget-spec-factory';

describeBudgetScreen({ name: 'BudgetComponent', component: BudgetComponent, fund: 'SKSP', withExpenseStructure: true });
