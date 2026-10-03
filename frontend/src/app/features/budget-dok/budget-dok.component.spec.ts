import { BudgetDokComponent } from './budget-dok.component';
import { describeBudgetScreen } from '../../testing/budget-spec-factory';

describeBudgetScreen({ name: 'BudgetDokComponent', component: BudgetDokComponent, fund: 'DOK', withExpenseStructure: false });
