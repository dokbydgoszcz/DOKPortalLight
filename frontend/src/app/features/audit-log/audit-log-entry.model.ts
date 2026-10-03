export type AuditResult = 'Allowed' | 'Blocked';

export interface AuditLogFilter {
  search: string;
  action: string;
  result: '' | AuditResult;
  from: string;
  to: string;
  take?: number;
}

export interface AuditLogEntry {
  id: string;
  timestampUtc: string;
  userId: string;
  userEmail: string;
  action: string;
  objectDescription: string;
  result: AuditResult;
}
