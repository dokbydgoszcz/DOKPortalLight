import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from './documents.service';
import { DownloadedFile, Letterhead } from './generated-document.model';
import { PdfPreviewComponent } from './pdf-preview.component';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

const SAMPLE = '<p style="text-align: right">{{pismo.miejscowość}}, dnia {{pismo.data słownie}}</p><p></p>' +
  '<h2 style="text-align: center">Przykładowe pismo</h2><p></p><p>Tak wygląda nagłówek i stopka na każdym piśmie z zaznaczonym „Papier firmowy”.</p>';

/** Wspólny nagłówek (logo + tekst) i stopka pism. */
@Component({
  selector: 'app-letterhead-settings',
  standalone: true,
  imports: [FormsModule, PdfPreviewComponent],
  template: `
    <div class="card">
      <div class="card-head"><h3>Nagłówek i stopka pism</h3></div>
      <div class="card-body head-body">
        <p class="small-muted">Drukowane na każdym piśmie, którego wzór ma zaznaczone „Papier firmowy”. Pierwszy wiersz nagłówka jest pogrubiony.</p>
        <div class="logo-row">
          <div class="logo-box">
            @if (logoUrl()) { <img [src]="logoUrl()" alt="Logo" /> } @else { <span class="small-muted">bez logo</span> }
          </div>
          <div class="logo-actions">
            <label class="btn ghost small" [class.disabled]="busy()">
              {{ logoUrl() ? 'Zmień logo' : 'Dodaj logo' }}
              <input type="file" accept="image/png,image/jpeg" hidden (change)="onLogo($event)" [disabled]="busy()" />
            </label>
            @if (logoUrl()) { <button class="btn ghost small danger" type="button" (click)="removeLogo()">Usuń logo</button> }
            <span class="small-muted">PNG albo JPG, do 2 MB.</span>
          </div>
        </div>
        <div class="form-grid">
          <div class="field full">
            <label for="lh-header">Tekst nagłówka (wiersze)</label>
            <textarea id="lh-header" name="headerText" rows="4" [ngModel]="headerText()" (ngModelChange)="headerText.set($event)" placeholder="Diecezjalny Ośrodek Katechumenalny&#10;ul. …, 85-… Bydgoszcz"></textarea>
          </div>
          <div class="field full">
            <label for="lh-footer">Stopka</label>
            <input id="lh-footer" name="footerText" [ngModel]="footerText()" (ngModelChange)="footerText.set($event)" placeholder="adres · telefon · e-mail · www" />
          </div>
          <div class="field">
            <label for="lh-place">Miejscowość wystawienia (pole „Miejscowość wystawienia”)</label>
            <input id="lh-place" name="place" [ngModel]="place()" (ngModelChange)="place.set($event)" placeholder="Bydgoszcz" />
          </div>
        </div>
        <div class="actions">
          <button class="btn ghost" type="button" (click)="showPreview()">Podgląd</button>
          <button class="btn primary" type="button" [disabled]="busy()" (click)="save()">Zapisz</button>
        </div>
      </div>
    </div>
    @if (preview(); as p) { <app-pdf-preview [blob]="p.blob" title="Podgląd nagłówka i stopki" (closed)="preview.set(null)" /> }
  `,
  styles: [`
    .head-body { display: grid; gap: 14px; }
    .head-body p { margin: 0; }
    .logo-row { display: flex; gap: 14px; align-items: center; flex-wrap: wrap; }
    .logo-box { width: 90px; height: 90px; border: 1px dashed var(--line, #dfe5e2); border-radius: 10px; display: grid; place-items: center; background: white; }
    .logo-box img { max-width: 80px; max-height: 80px; }
    .logo-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .actions { display: flex; gap: 8px; }
  `]
})
export class LetterheadSettingsComponent implements OnInit, OnDestroy {
  readonly headerText = signal('');
  readonly footerText = signal('');
  readonly place = signal('');
  readonly logoUrl = signal<string | null>(null);
  readonly busy = signal(false);
  readonly preview = signal<DownloadedFile | null>(null);

  constructor(private readonly documents: DocumentsService, private readonly toast: ToastService) {}

  ngOnInit(): void {
    this.documents.letterhead().subscribe({
      next: head => this.apply(head),
      error: () => this.toast.error('Nie udało się wczytać nagłówka pism.')
    });
  }

  ngOnDestroy(): void {
    this.setLogo(null);
  }

  private apply(head: Letterhead): void {
    this.headerText.set(head.headerText ?? '');
    this.footerText.set(head.footerText ?? '');
    this.place.set(head.place ?? '');
    if (head.hasLogo) this.documents.logo().subscribe({ next: blob => this.setLogo(blob), error: () => this.setLogo(null) });
    else this.setLogo(null);
  }

  private setLogo(blob: Blob | null): void {
    const previous = this.logoUrl();
    if (previous) URL.revokeObjectURL(previous);
    this.logoUrl.set(blob ? URL.createObjectURL(blob) : null);
  }

  save(): void {
    this.busy.set(true);
    this.documents.saveLetterhead({ headerText: this.headerText(), footerText: this.footerText(), place: this.place() }).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success('Zapisano nagłówek i stopkę.');
      },
      error: err => {
        this.busy.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się zapisać nagłówka.'));
      }
    });
  }

  onLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!/^image\/(png|jpeg)$/.test(file.type)) {
      this.toast.error('Logo musi być obrazem PNG albo JPG.');
      return;
    }
    this.busy.set(true);
    this.documents.uploadLogo(file).subscribe({
      next: () => {
        this.busy.set(false);
        this.setLogo(file);
        this.toast.success('Zapisano logo.');
      },
      error: err => {
        this.busy.set(false);
        this.toast.error(serverMessage(err, 'Nie udało się zapisać logo.'));
      }
    });
  }

  removeLogo(): void {
    if (!confirm('Usunąć logo z nagłówka pism?')) return;
    this.documents.deleteLogo().subscribe({
      next: () => {
        this.setLogo(null);
        this.toast.success('Usunięto logo.');
      },
      error: err => this.toast.error(serverMessage(err, 'Nie udało się usunąć logo.'))
    });
  }

  /** Podgląd przykładowego pisma z nagłówkiem – po zapisaniu zmian. */
  showPreview(): void {
    this.documents.preview(SAMPLE, true, null).subscribe({
      next: file => this.preview.set(file),
      error: err => this.toast.error(serverMessage(err, 'Nie udało się przygotować podglądu.'))
    });
  }
}
