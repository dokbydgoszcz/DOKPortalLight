import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuditLogEntry, AuditLogFilter } from './audit-log-entry.model';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/audit-log`;

  constructor(private readonly http: HttpClient) {}

  list(filter: Partial<AuditLogFilter> = {}) {
    const params: Record<string, string> = {};
    const search = filter.search?.trim();
    if (search) params['search'] = search;
    if (filter.action) params['action'] = filter.action;
    if (filter.result) params['result'] = filter.result;
    if (filter.from) params['from'] = filter.from;
    if (filter.to) params['to'] = filter.to;
    if (filter.take) params['take'] = String(filter.take);
    return this.http.get<AuditLogEntry[]>(this.baseUrl, { params });
  }

  actions() {
    return this.http.get<string[]>(`${this.baseUrl}/actions`);
  }
}
