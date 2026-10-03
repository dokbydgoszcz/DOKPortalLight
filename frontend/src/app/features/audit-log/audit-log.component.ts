import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditLogService } from './audit-log.service';
import { AuditLogEntry, AuditLogFilter } from './audit-log-entry.model';
import { ToastService } from '../../core/notifications/toast.service';

const TAKE = 500;
const EMPTY_FILTER: AuditLogFilter = { search: '', action: '', result: '', from: '', to: '' };

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  readonly entries = signal<AuditLogEntry[]>([]);
  readonly actions = signal<string[]>([]);
  readonly take = TAKE;
  filter: AuditLogFilter = { ...EMPTY_FILTER };
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly auditLogService: AuditLogService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
    // Lista akcji to tylko podpowiedź do filtra – gdy się nie wczyta, filtr działa bez niej.
    this.auditLogService.actions().subscribe({ next: actions => this.actions.set(actions), error: () => {} });
  }

  load(): void {
    this.auditLogService.list({ ...this.filter, take: TAKE }).subscribe({
      next: entries => this.entries.set(entries),
      error: () => this.toast.error('Nie udało się wczytać dziennika audytu.')
    });
  }

  /** Wyszukiwanie tekstowe z krótkim opóźnieniem, żeby nie odpytywać serwera przy każdym znaku. */
  onSearchChange(): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => this.load(), 300);
  }

  clearFilters(): void {
    this.filter = { ...EMPTY_FILTER };
    this.load();
  }

  exportCsv(): void {
    const header = 'Data,Uzytkownik,Akcja,Obiekt,Wynik';
    const quote = (value: string) => (/[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value);
    const rows = this.entries().map(e =>
      [e.timestampUtc, e.userEmail, e.action, e.objectDescription, e.result].map(quote).join(',')
    );
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
