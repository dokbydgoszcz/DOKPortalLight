import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';
import { MfaSettingsComponent } from './mfa-settings.component';

/** Zmiana własnego hasła; obowiązkowa, gdy hasło nadał administrator. */
@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [FormsModule, MfaSettingsComponent],
  template: `
    <div class="page-heading">
      <div><h2>Moje konto</h2><p>Konto: {{ auth.email() }}</p></div>
    </div>
    <div class="account-grid">
    <section class="card">
      <div class="card-head"><h3>Zmiana hasła</h3></div>
      <div class="card-body password-body">
      @if (auth.mustChangePassword()) {
        <p class="must-change" role="status">
          Hasło nadał administrator. Ustaw własne hasło, którego nikt poza Tobą nie zna – potem zobaczysz resztę portalu.
        </p>
      }
      <form (ngSubmit)="submit()" novalidate>
        <div class="field">
          <label for="current-password">Obecne hasło <span class="required">*</span></label>
          <input id="current-password" name="current" type="password" autocomplete="current-password" [(ngModel)]="current" />
        </div>
        <div class="field">
          <label for="new-password">Nowe hasło <span class="required">*</span></label>
          <input id="new-password" name="new" type="password" autocomplete="new-password" [(ngModel)]="next" />
          <span class="form-hint">Co najmniej 8 znaków.</span>
        </div>
        <div class="field">
          <label for="repeat-password">Powtórz nowe hasło <span class="required">*</span></label>
          <input id="repeat-password" name="repeat" type="password" autocomplete="new-password" [(ngModel)]="repeat" />
        </div>
        @if (problem) {
          <div class="form-error" role="alert">{{ problem }}</div>
        } @else if (error()) {
          <div class="form-error" role="alert">{{ error() }}</div>
        }
        <button class="btn primary" type="submit" [disabled]="!canSubmit || isSaving()">
          {{ isSaving() ? 'Zapisywanie…' : 'Zmień hasło' }}
        </button>
      </form>
      </div>
    </section>
    @if (!auth.mustChangePassword()) {
      <app-mfa-settings />
    }
    </div>
  `,
  styles: [`
    .account-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 420px)); gap: 16px; align-items: start; }
    .account-grid .card { margin: 0; }
    .password-body, form { display: grid; gap: 14px; }
    .field .form-hint { display: block; margin-top: 6px; }
    .must-change { margin: 0; padding: 10px 12px; border-radius: 10px; background: var(--warn-bg, #fff6e0); font-size: 13px; }
    .form-error { color: var(--danger); font-size: 13px; }
    button { justify-self: start; }
  `]
})
export class ChangePasswordComponent {
  current = '';
  next = '';
  repeat = '';
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);

  constructor(
    readonly auth: AuthService,
    private readonly router: Router,
    private readonly toast: ToastService
  ) {}

  /** Błąd widoczny od razu, bez wysyłania (zbyt krótkie albo różne hasła). */
  get problem(): string | null {
    if (this.next && this.next.length < 8) return 'Nowe hasło musi mieć co najmniej 8 znaków.';
    if (this.repeat && this.next !== this.repeat) return 'Powtórzone hasło jest inne niż nowe.';
    if (this.next && this.next === this.current) return 'Nowe hasło musi się różnić od obecnego.';
    return null;
  }

  get canSubmit(): boolean {
    return !!this.current && !!this.next && this.next === this.repeat && !this.problem;
  }

  async submit(): Promise<void> {
    if (!this.canSubmit || this.isSaving()) return;
    this.error.set(null);
    this.isSaving.set(true);
    try {
      await this.auth.changePassword(this.current, this.next);
      this.toast.success('Hasło zostało zmienione.');
      await this.router.navigateByUrl('/dashboard');
    } catch (err) {
      this.error.set(serverMessage(err, 'Nie udało się zmienić hasła.'));
    } finally {
      this.isSaving.set(false);
    }
  }
}
