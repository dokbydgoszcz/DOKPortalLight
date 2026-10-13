import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { BudgetEntry, BudgetFund, BudgetPeriod, BudgetSummary, CreateBudgetEntryValue } from './budget-entry.model';

@Injectable({ providedIn: 'root' })
export class BudgetService {
  readonly baseUrl = `${environment.apiBaseUrl}/api/budget`;

  constructor(private readonly http: HttpClient) {}

  private params(fund: BudgetFund, period: BudgetPeriod = {}): Record<string, string> {
    const params: Record<string, string> = { fund };
    if (period.from) params['from'] = period.from;
    if (period.to) params['to'] = period.to;
    return params;
  }

  listEntries(fund: BudgetFund, period: BudgetPeriod = {}) {
    return this.http.get<BudgetEntry[]>(this.baseUrl, { params: this.params(fund, period) });
  }

  summary(fund: BudgetFund, period: BudgetPeriod) {
    return this.http.get<BudgetSummary>(`${this.baseUrl}/summary`, { params: this.params(fund, period) });
  }

  export(fund: BudgetFund, period: BudgetPeriod) {
    return this.http.get(`${this.baseUrl}/export`, { params: this.params(fund, period), responseType: 'blob' });
  }

  create(value: CreateBudgetEntryValue) {
    return this.http.post<BudgetEntry>(this.baseUrl, value);
  }

  update(id: string, value: Omit<CreateBudgetEntryValue, 'fund'>) {
    return this.http.put<BudgetEntry>(`${this.baseUrl}/${id}`, value);
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
