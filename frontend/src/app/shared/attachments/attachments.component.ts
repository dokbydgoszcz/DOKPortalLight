import { Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HasPermissionDirective } from '../permissions/has-permission.directive';
import { ToastService } from '../../core/notifications/toast.service';
import { Attachment } from './attachment.model';
import { AttachmentsService } from './attachments.service';
import { ACCEPT_ATTRIBUTE, RULES_HINT, formatFileSize, saveBlob } from './attachment-rules';

/** Lista plików dołączonych do rekordu (notatki, superwizji) z pobieraniem, usuwaniem i dodawaniem wielu plików naraz. */
@Component({
  selector: 'app-attachments',
  standalone: true,
  imports: [DatePipe, HasPermissionDirective],
  template: `
    <div class="attachments">
      @for (attachment of attachments(); track attachment.id) {
        <div class="attachment-row">
          <span class="attachment-name">📎 {{ attachment.fileName }}</span>
          <span class="attachment-meta">{{ formatSize(attachment.sizeBytes) }} · {{ attachment.uploadedAtUtc | date: 'dd.MM.yyyy HH:mm' }}</span>
          <span class="link" (click)="download(attachment)">Pobierz</span>
          <span *appHasPermission="managePermission()" class="link danger" (click)="remove(attachment)">Usuń</span>
        </div>
      } @empty {
        @if (showEmpty()) {
          <div class="attachment-meta">Brak załączników.</div>
        }
      }
      <div *appHasPermission="managePermission()" class="attachment-add">
        <label class="btn ghost small">
          ＋ Dodaj plik
          <input type="file" multiple name="attachmentFiles" style="display:none" [accept]="accept" (change)="onFilesSelected($event)">
        </label>
        @if (showHint()) {
          <span class="attachment-meta">{{ hint }}</span>
        }
      </div>
    </div>
  `,
  styles: `
    .attachments { display: flex; flex-direction: column; gap: 6px; margin-top: 8px; }
    .attachment-row { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 12px; font-size: 12px; }
    .attachment-name { font-weight: 600; overflow-wrap: anywhere; }
    .attachment-meta { font-size: 11px; color: var(--muted); }
    .attachment-add { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-top: 2px; }
    .link.danger { color: var(--danger); }
  `
})
export class AttachmentsComponent {
  readonly attachments = input.required<Attachment[]>();
  /** Adres bazowy załączników właściciela, np. …/api/supervisions/{id}/attachments. */
  readonly baseUrl = input.required<string>();
  /** Uprawnienie wymagane do dodawania i usuwania; pobieranie ma każdy, kto widzi listę. */
  readonly managePermission = input.required<string>();
  readonly showEmpty = input(false);
  readonly showHint = input(true);
  /** Wywoływane po dodaniu lub usunięciu plików – właściciel odświeża swoją listę. */
  readonly changed = output<void>();

  readonly accept = ACCEPT_ATTRIBUTE;
  readonly hint = RULES_HINT;
  readonly formatSize = formatFileSize;

  constructor(
    private readonly attachmentsService: AttachmentsService,
    private readonly toast: ToastService
  ) {}

  download(attachment: Attachment): void {
    this.attachmentsService.download(this.baseUrl(), attachment.id).subscribe({
      next: blob => saveBlob(blob, attachment.fileName),
      error: () => this.toast.error('Nie udało się pobrać pliku.')
    });
  }

  remove(attachment: Attachment): void {
    if (!confirm(`Usunąć załącznik „${attachment.fileName}”?`)) return;
    this.attachmentsService.remove(this.baseUrl(), attachment.id).subscribe({
      next: () => {
        this.toast.success('Załącznik usunięty.');
        this.changed.emit();
      },
      error: () => this.toast.error('Nie udało się usunąć załącznika.')
    });
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (files.length === 0) return;

    this.attachmentsService.uploadMany(this.baseUrl(), files).subscribe(summary => {
      input.value = '';
      summary.errors.forEach(message => this.toast.error(message));
      if (summary.uploaded > 0) {
        this.toast.success(summary.uploaded === 1 ? 'Dodano plik.' : `Dodano pliki: ${summary.uploaded}.`);
        this.changed.emit();
      }
    });
  }
}
