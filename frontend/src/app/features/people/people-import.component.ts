import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { FUNCTION_LABELS, FUNCTION_TYPES, FunctionType } from './person.model';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

interface ImportRow {
  line: number;
  firstName: string;
  lastName: string;
  email: string | null;
  phone: string | null;
  newPerson: boolean;
  hadFunction: boolean;
  candidateYear: number | null;
  warnings: string[];
}

export interface PeopleImportResult {
  applied: boolean;
  newPeople: number;
  existingPeople: number;
  functionsGranted: number;
  candidatesEnrolled: number;
  skippedLines: string[];
  rows: ImportRow[];
}

/** Import wklejonej listy osób (np. absolwentów albo uczniów SKŚP) z podglądem przed zapisem. */
@Component({
  selector: 'app-people-import',
  standalone: true,
  template: `
    <div class="overlay show">
      <div class="modal import-modal">
        <div class="modal-head">
          <h3>Import listy osób</h3>
          <button class="close" type="button" (click)="closed.emit()">×</button>
        </div>
        <div class="modal-body">
          <div class="field full">
            <label for="import-text">Lista osób – jedna w wierszu</label>
            <textarea id="import-text" name="text" rows="9" [value]="text()" (input)="change(() => text.set($any($event.target).value))"
              placeholder="1 Kowalska Anna&#10;2 Nowak Jan&#10;&#10;Można wkleić kolumny z Excela (nazwisko, imię, e-mail, telefon) i nagłówki „Rok II”, „Rok III”."></textarea>
            <span class="form-hint">Numery i nagłówki („Nazwisko i imię”, nazwa szkoły) są pomijane. Po tabulatorze albo średniku można podać e-mail i telefon.</span>
          </div>
          <div class="options">
            <label class="checkbox-label"><input type="radio" name="order" [checked]="!firstNameFirst()" (change)="change(() => firstNameFirst.set(false))" /> Nazwisko Imię</label>
            <label class="checkbox-label"><input type="radio" name="order" [checked]="firstNameFirst()" (change)="change(() => firstNameFirst.set(true))" /> Imię Nazwisko</label>
          </div>
          <div class="options">
            <label class="checkbox-label">
              <input type="checkbox" name="grant" [checked]="grant()" (change)="change(() => grant.set($any($event.target).checked))" /> Nadaj funkcję
            </label>
            <select name="function" [disabled]="!grant()" (change)="change(() => functionType.set($any($event.target).value))">
              @for (t of types; track t) { <option [value]="t" [selected]="t === functionType()">{{ labels[t] }}</option> }
            </select>
            <input name="note" [disabled]="!grant()" placeholder="Notatka przy funkcji, np. Absolwent SKŚP 2023–2025" [value]="note()"
              (input)="change(() => note.set($any($event.target).value))" />
          </div>
          <div class="options">
            <label class="checkbox-label">
              <input type="checkbox" name="enroll" [checked]="enroll()" (change)="change(() => enroll.set($any($event.target).checked))" /> Wpisz jako kandydatów SKŚP
            </label>
            <select name="year" [disabled]="!enroll()" (change)="change(() => year.set(+$any($event.target).value || null))">
              <option value="" [selected]="year() === null">rok z nagłówków „Rok …”</option>
              @for (y of [1, 2, 3]; track y) { <option [value]="y" [selected]="year() === y">rok {{ y }}</option> }
            </select>
          </div>

          @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
          @if (preview(); as p) {
            <div class="import-summary" role="status">
              <span>osób na liście: <b>{{ p.rows.length }}</b></span>
              <span>nowe: <b>{{ p.newPeople }}</b></span>
              <span>już w bazie: <b>{{ p.existingPeople }}</b></span>
              @if (grant()) { <span>nadanie funkcji: <b>{{ p.functionsGranted }}</b></span> }
              @if (enroll()) { <span>nowi kandydaci SKŚP: <b>{{ p.candidatesEnrolled }}</b></span> }
            </div>
            @if (p.skippedLines.length) {
              <p class="small-muted">Pominięte wiersze (nagłówki): {{ p.skippedLines.join(' · ') }}</p>
            }
            <div class="table-wrap import-table">
              <table>
                <thead><tr><th>Nazwisko</th><th>Imię</th><th>W bazie</th><th>Zmiana</th></tr></thead>
                <tbody>
                  @for (r of p.rows; track r.line) {
                    <tr>
                      <td><b>{{ r.lastName }}</b></td>
                      <td>{{ r.firstName }} @if (r.email || r.phone) { <div class="small-muted">{{ r.email }} {{ r.phone }}</div> }</td>
                      <td>@if (r.newPerson) { <span class="pill blue">nowa osoba</span> } @else { <span class="pill">jest</span> }</td>
                      <td>
                        @if (grant()) { <div>{{ r.hadFunction ? 'ma już funkcję' : '＋ ' + labels[functionType()] }}</div> }
                        @if (r.candidateYear) { <div>＋ kandydat SKŚP, rok {{ r.candidateYear }}</div> }
                        @for (w of r.warnings; track w) { <div class="warning">⚠ {{ w }}</div> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
        <div class="modal-foot">
          <button class="btn ghost" type="button" (click)="closed.emit()">Anuluj</button>
          <button class="btn ghost" type="button" [disabled]="busy() || !text().trim()" (click)="run(false)">Sprawdź</button>
          <button class="btn primary" type="button" [disabled]="busy() || !preview()" (click)="run(true)">Importuj</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .import-modal { max-width: 860px; }
    textarea { width: 100%; font-family: inherit; }
    .options { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; margin: 10px 0; font-size: 13px; }
    .options select, .options input[name="note"] { width: auto; min-width: 180px; }
    .options input[name="note"] { flex: 1; }
    .import-summary { display: flex; flex-wrap: wrap; gap: 6px 18px; margin: 14px 0 8px; font-size: 13px; }
    .import-table { max-height: 45vh; overflow: auto; }
    .warning { margin-top: 3px; color: #9a6a04; font-size: 12px; }
    .form-error { color: var(--danger); margin-top: 10px; }
  `]
})
export class PeopleImportComponent {
  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly imported = new EventEmitter<PeopleImportResult>();

  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  readonly labels = FUNCTION_LABELS;
  readonly types = FUNCTION_TYPES;

  readonly text = signal('');
  readonly firstNameFirst = signal(false);
  readonly grant = signal(true);
  readonly functionType = signal<FunctionType>('Catechist');
  readonly note = signal('');
  readonly enroll = signal(false);
  readonly year = signal<number | null>(null);
  readonly preview = signal<PeopleImportResult | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  /** Każda zmiana ustawień unieważnia podgląd – import idzie tylko po świeżym „Sprawdź”. */
  change(apply: () => void): void {
    apply();
    this.preview.set(null);
  }

  run(apply: boolean): void {
    this.busy.set(true);
    this.error.set(null);
    const body = {
      text: this.text(),
      firstNameFirst: this.firstNameFirst(),
      function: this.grant() ? this.functionType() : null,
      functionNote: this.grant() && this.note().trim() ? this.note().trim() : null,
      enrollAsCandidates: this.enroll(),
      candidateYear: this.enroll() ? this.year() : null
    };
    this.http.post<PeopleImportResult>(`${environment.apiBaseUrl}/api/people/import${apply ? '' : '/preview'}`, body).subscribe({
      next: result => {
        this.busy.set(false);
        if (apply) {
          const parts = [`${result.newPeople} nowych osób`];
          if (this.grant()) parts.push(`${result.functionsGranted} nadanych funkcji`);
          if (this.enroll()) parts.push(`${result.candidatesEnrolled} nowych kandydatów SKŚP`);
          this.toast.success(`Zaimportowano: ${parts.join(', ')}.`);
          this.imported.emit(result);
        } else {
          this.preview.set(result);
        }
      },
      error: err => {
        this.busy.set(false);
        this.error.set(serverMessage(err, apply ? 'Import się nie udał.' : 'Nie udało się sprawdzić listy.'));
      }
    });
  }
}
