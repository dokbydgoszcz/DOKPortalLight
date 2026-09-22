export type AuditResult = 'Allowed' | 'Blocked';

export interface AuditLogEntry {
  id: string;
  timestampUtc: string;
  userId: string;
  userEmail: string;
  action: string;
  objectDescription: string;
  result: AuditResult;
}
