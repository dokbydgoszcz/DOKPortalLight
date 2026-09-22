import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { BudgetEntry, BudgetFund, CreateBudgetEntryValue } from './budget-entry.model';

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/budget`;

  constructor(private readonly http: HttpClient) {}

  listEntries(fund: BudgetFund) {
    return this.http.get<BudgetEntry[]>(this.baseUrl, { params: { fund } });
  }

  create(value: CreateBudgetEntryValue) {
    return this.http.post<BudgetEntry>(this.baseUrl, value);
  }
}
