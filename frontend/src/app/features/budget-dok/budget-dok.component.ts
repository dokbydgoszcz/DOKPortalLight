import { Component } from '@angular/core';
import { BudgetLedgerComponent } from '../budget/budget-ledger.component';
import { Permissions } from '../../core/auth/permissions';

@Component({
  selector: 'app-budget-dok',
  standalone: true,
  imports: [BudgetLedgerComponent],
  template: `<app-budget-ledger fund="DOK" heading="Budżet DOK" subtitle="Dedykowana ewidencja przychodów i wydatków Ośrodka." [managePermission]="manage" />`
})
export class BudgetDokComponent {
  readonly manage = Permissions.BudgetDokManage;
}
