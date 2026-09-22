import { Component, OnInit, signal } from '@angular/core';
import { AuditLogService } from './audit-log.service';
import { AuditLogEntry } from './audit-log-entry.model';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  readonly entries = signal<AuditLogEntry[]>([]);

  constructor(private readonly auditLogService: AuditLogService) {}

  ngOnInit(): void {
    this.auditLogService.list().subscribe(entries => this.entries.set(entries));
  }

  exportCsv(): void {
    const header = 'Data,Uzytkownik,Akcja,Obiekt,Wynik';
    const rows = this.entries().map(e => [e.timestampUtc, e.userEmail, e.action, e.objectDescription, e.result].join(','));
    const csv = [header, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'audit-log.csv';
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
