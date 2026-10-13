import { Component } from '@angular/core';
import { BudgetLedgerComponent } from './budget-ledger.component';
import { Permissions } from '../../core/auth/permissions';

@Component({
  selector: 'app-budget',
  standalone: true,
  imports: [BudgetLedgerComponent],
  template: `<app-budget-ledger fund="SKSP" heading="Budżet SKŚP" subtitle="Przychody, wydatki i zestawienie kategorii." [managePermission]="manage" />`
})
export class BudgetComponent {
  readonly manage = Permissions.BudgetSkspManage;
}
