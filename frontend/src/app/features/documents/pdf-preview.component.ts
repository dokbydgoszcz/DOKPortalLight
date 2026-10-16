import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

/** Podgląd PDF w okienku (bez otwierania nowej karty). */
@Component({
  selector: 'app-pdf-preview',
  standalone: true,
  template: `
    <div class="overlay show">
      <div class="modal preview-modal">
        <div class="modal-head">
          <h3>{{ title }}</h3>
          <button class="close" type="button" (click)="closed.emit()">×</button>
        </div>
        <div class="modal-body preview-body">
          @if (missing.length) {
            <div class="note">Brak danych w polach: <b>{{ missing.join(', ') }}</b> – w piśmie są kropki do wypełnienia.</div>
          }
          @if (safeUrl) { <iframe [src]="safeUrl" title="Podgląd pisma"></iframe> }
        </div>
        <div class="modal-foot">
          <button class="btn ghost" type="button" (click)="closed.emit()">Zamknij</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .preview-modal { max-width: 900px; width: 96vw; }
    .preview-body { display: grid; gap: 10px; }
    iframe { width: 100%; height: 70vh; border: 1px solid var(--line, #dfe5e2); border-radius: 8px; background: #f3f5f4; }
  `]
})
export class PdfPreviewComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) blob!: Blob;
  @Input() title = 'Podgląd';
  @Input() missing: string[] = [];
  @Output() readonly closed = new EventEmitter<void>();

  private readonly sanitizer = inject(DomSanitizer);
  private url: string | null = null;
  safeUrl: SafeResourceUrl | null = null;

  ngOnChanges(): void {
    this.release();
    this.url = URL.createObjectURL(new Blob([this.blob], { type: 'application/pdf' }));
    this.safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.url);
  }

  ngOnDestroy(): void {
    this.release();
  }

  private release(): void {
    if (this.url) URL.revokeObjectURL(this.url);
    this.url = null;
  }
}
