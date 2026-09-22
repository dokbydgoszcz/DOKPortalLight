import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuditLogEntry } from './audit-log-entry.model';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/audit-log`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<AuditLogEntry[]>(this.baseUrl);
  }
}
