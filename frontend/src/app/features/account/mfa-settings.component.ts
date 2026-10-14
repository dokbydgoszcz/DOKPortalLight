import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as QRCode from 'qrcode';
import { AuthService, MfaSetup, MfaStatus } from '../../core/auth/auth.service';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

/** Weryfikacja dwuetapowa aplikacją uwierzytelniającą (Microsoft/Google Authenticator): włączenie z kodem QR, kody zapasowe, wyłączenie. */
@Component({
  selector: 'app-mfa-settings',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="card">
      <div class="card-head">
        <h3>Weryfikacja dwuetapowa (MFA)</h3>
        @if (status()?.enabled) { <span class="pill green">włączona</span> }
      </div>
      <div class="card-body mfa-body">
      @if (error()) {
        <div class="form-error" role="alert">{{ error() }}</div>
      }
      @if (recoveryCodes(); as codes) {
        <div class="codes" role="status">
          <p><b>Weryfikacja dwuetapowa włączona.</b> Zapisz kody zapasowe w bezpiecznym miejscu (np. wydrukuj). Każdy działa raz –
            zamiast kodu z aplikacji, gdy nie masz telefonu. Nie pokażemy ich ponownie.</p>
          <ul class="code-list">
            @for (code of codes; track code) { <li>{{ code }}</li> }
          </ul>
          <div class="row">
            <button class="btn ghost small" type="button" (click)="copyCodes(codes)">Kopiuj kody</button>
            <button class="btn primary small" type="button" (click)="recoveryCodes.set(null)">Zapisałem kody</button>
          </div>
        </div>
      } @else if (setup(); as s) {
        <ol class="steps">
          <li>Zainstaluj na telefonie aplikację <b>Microsoft Authenticator</b> (lub Google Authenticator).</li>
          <li>W aplikacji wybierz „Dodaj konto” → „Inne konto” i zeskanuj kod QR:</li>
        </ol>
        @if (qrDataUrl()) {
          <img class="qr" [src]="qrDataUrl()" alt="Kod QR do aplikacji uwierzytelniającej" width="200" height="200" />
        }
        <p class="small-muted">Nie możesz zeskanować? Wpisz ręcznie klucz: <code class="key">{{ groupedKey(s.sharedKey) }}</code></p>
        <div class="field">
          <label for="mfa-setup-code">3. Wpisz 6-cyfrowy kod z aplikacji</label>
          <input id="mfa-setup-code" name="setupCode" inputmode="numeric" autocomplete="one-time-code" maxlength="6" placeholder="123456" [(ngModel)]="code" />
        </div>
        <div class="row">
          <button class="btn primary" type="button" [disabled]="busy() || code.trim().length !== 6" (click)="enable()">Włącz weryfikację</button>
          <button class="btn ghost" type="button" (click)="cancelSetup()">Anuluj</button>
        </div>
      } @else if (status(); as st) {
        @if (st.enabled) {
          <p>Weryfikacja dwuetapowa jest <b>włączona</b>. Przy logowaniu, po haśle, podajesz kod z aplikacji.
            Pozostałe kody zapasowe: {{ st.recoveryCodesLeft }}.</p>
          <div class="field">
            <label for="mfa-disable-code">Aby wyłączyć, podaj kod z aplikacji</label>
            <input id="mfa-disable-code" name="disableCode" inputmode="numeric" autocomplete="one-time-code" maxlength="6" placeholder="123456" [(ngModel)]="code" />
          </div>
          <button class="btn ghost danger" type="button" [disabled]="busy() || code.trim().length !== 6" (click)="disable()">Wyłącz weryfikację dwuetapową</button>
        } @else {
          <p>Zabezpiecz konto drugim krokiem logowania: po haśle podasz kod z aplikacji w telefonie (np. Microsoft Authenticator).
            Nawet gdy ktoś pozna hasło, bez telefonu się nie zaloguje.</p>
          <button class="btn primary" type="button" [disabled]="busy()" (click)="start()">Włącz weryfikację dwuetapową</button>
        }
      }
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .card { margin: 0; }
    .mfa-body { display: grid; gap: 14px; font-size: 13px; line-height: 1.5; }
    p { margin: 0; color: var(--muted); }
    p b { color: var(--text); }
    .steps { margin: 0; padding-left: 18px; display: grid; gap: 4px; }
    .qr { justify-self: start; border: 1px solid var(--line, #e5e7eb); border-radius: 8px; }
    .key { font-size: 13px; letter-spacing: 1px; overflow-wrap: anywhere; }
    .row { display: flex; gap: 8px; flex-wrap: wrap; }
    .code-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4px 16px; margin: 8px 0; padding-left: 18px; font-family: monospace; }
    .codes { display: grid; gap: 8px; }
    .form-error { color: var(--danger); }
    .danger { color: var(--danger); }
    button { justify-self: start; }
  `]
})
export class MfaSettingsComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly status = signal<MfaStatus | null>(null);
  readonly setup = signal<MfaSetup | null>(null);
  readonly qrDataUrl = signal<string | null>(null);
  readonly recoveryCodes = signal<string[] | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  code = '';

  ngOnInit(): void {
    void this.loadStatus();
  }

  async loadStatus(): Promise<void> {
    try {
      this.status.set(await this.auth.mfaStatus());
    } catch (err) {
      this.error.set(serverMessage(err, 'Nie udało się sprawdzić weryfikacji dwuetapowej.'));
    }
  }

  /** Klucz w grupach po 4 znaki – łatwiej przepisać do aplikacji. */
  groupedKey(key: string): string {
    return key.replace(/(.{4})/g, '$1 ').trim();
  }

  async start(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const setup = await this.auth.setupMfa();
      this.code = '';
      this.setup.set(setup);
      this.qrDataUrl.set(await QRCode.toDataURL(setup.otpAuthUri, { width: 200, margin: 1 }));
    } catch (err) {
      this.error.set(serverMessage(err, 'Nie udało się rozpocząć konfiguracji.'));
    } finally {
      this.busy.set(false);
    }
  }

  cancelSetup(): void {
    this.setup.set(null);
    this.qrDataUrl.set(null);
    this.code = '';
  }

  async enable(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const codes = await this.auth.enableMfa(this.code.trim());
      this.cancelSetup();
      this.recoveryCodes.set(codes);
      await this.loadStatus();
    } catch (err) {
      this.error.set(serverMessage(err, 'Nieprawidłowy kod.'));
    } finally {
      this.busy.set(false);
    }
  }

  async disable(): Promise<void> {
    if (!confirm('Wyłączyć weryfikację dwuetapową? Do logowania wystarczy wtedy samo hasło.')) return;
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.disableMfa(this.code.trim());
      this.code = '';
      this.toast.success('Weryfikacja dwuetapowa wyłączona.');
      await this.loadStatus();
    } catch (err) {
      this.error.set(serverMessage(err, 'Nieprawidłowy kod.'));
    } finally {
      this.busy.set(false);
    }
  }

  async copyCodes(codes: string[]): Promise<void> {
    try {
      await navigator.clipboard.writeText(codes.join('\n'));
      this.toast.success('Skopiowano kody zapasowe.');
    } catch {
      this.toast.error('Nie udało się skopiować – przepisz kody ręcznie.');
    }
  }
}
