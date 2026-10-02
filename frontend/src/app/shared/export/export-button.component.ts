import { Component, Input, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { EXPORT_LISTS, ExportListKey } from './export-lists';
import { ExportService } from './export.service';

@Component({
  selector: 'app-export-button',
  standalone: true,
  template: `
    @if (canExport()) {
      <button class="btn ghost" [disabled]="exporting()" (click)="export()">
        {{ exporting() ? 'Pobieranie…' : 'Eksportuj do Excela' }}
      </button>
    }
  `
})
export class ExportButtonComponent {
  @Input({ required: true }) list!: ExportListKey;

  readonly exporting = signal(false);

  constructor(
    private readonly auth: AuthService,
    private readonly exportService: ExportService
  ) {}

  canExport(): boolean {
    return this.auth.hasAnyRole(EXPORT_LISTS[this.list].roles);
  }

  export(): void {
    const config = EXPORT_LISTS[this.list];
    this.exporting.set(true);
    this.exportService.download(config.path, config.fileName).subscribe({
      complete: () => this.exporting.set(false)
    });
  }
}
