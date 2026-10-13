import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { AuthService } from '../../core/auth/auth.service';
import { serverMessage } from '../../shared/http-error';

/** Ustawienie nowego hasła z linku „Nie pamiętam hasła” (strona bez logowania). */
@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <main class="reset-page">
      <div class="card reset-card">
        <h2>Nowe hasło</h2>
        @if (done()) {
          <p role="status">Hasło zostało ustawione. Możesz się zalogować.</p>
          <a class="btn primary" routerLink="/login">Przejdź do logowania</a>
        } @else if (!userId || !token) {
          <p class="form-error" role="alert">Link jest niepełny. Skopiuj go z e-maila w całości albo poproś o nowy na stronie logowania.</p>
          <a class="btn ghost" routerLink="/login">Strona logowania</a>
        } @else {
          <form (ngSubmit)="submit()" novalidate>
            <div class="field">
              <label for="reset-new">Nowe hasło <span class="required">*</span></label>
              <input id="reset-new" name="new" type="password" autocomplete="new-password" [(ngModel)]="next" />
              <span class="form-hint">Co najmniej 8 znaków.</span>
            </div>
            <div class="field">
              <label for="reset-repeat">Powtórz nowe hasło <span class="required">*</span></label>
              <input id="reset-repeat" name="repeat" type="password" autocomplete="new-password" [(ngModel)]="repeat" />
            </div>
            @if (problem) {
              <div class="form-error" role="alert">{{ problem }}</div>
            } @else if (error()) {
              <div class="form-error" role="alert">{{ error() }}</div>
            }
            <button class="btn primary" type="submit" [disabled]="!canSubmit || isSaving()">
              {{ isSaving() ? 'Zapisywanie…' : 'Ustaw hasło' }}
            </button>
          </form>
        }
      </div>
    </main>
  `,
  styles: [`
    .reset-page { min-height: 100vh; display: grid; place-items: center; padding: 16px; background: var(--bg, #f5f6f8); }
    .reset-card { width: 100%; max-width: 420px; display: grid; gap: 12px; }
    form { display: grid; gap: 12px; }
    .form-error { color: var(--danger); }
    h2 { margin: 0; }
  `]
})
export class ResetPasswordComponent implements OnInit {
  userId = '';
  token = '';
  next = '';
  repeat = '';
  readonly isSaving = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly title: Title
  ) {}

  ngOnInit(): void {
    this.title.setTitle('Nowe hasło | DOK Portal');
    const params = this.route.snapshot.queryParamMap;
    this.userId = params.get('uid') ?? '';
    this.token = params.get('token') ?? '';
    // Kod nie powinien zostawać w historii przeglądarki ani w pasku adresu.
    if (this.userId && this.token) {
      void this.router.navigate([], { queryParams: {}, replaceUrl: true });
    }
  }

  get problem(): string | null {
    if (this.next && this.next.length < 8) return 'Nowe hasło musi mieć co najmniej 8 znaków.';
    if (this.repeat && this.next !== this.repeat) return 'Powtórzone hasło jest inne niż nowe.';
    return null;
  }

  get canSubmit(): boolean {
    return !!this.next && this.next === this.repeat && !this.problem;
  }

  async submit(): Promise<void> {
    if (!this.canSubmit || this.isSaving()) return;
    this.error.set(null);
    this.isSaving.set(true);
    try {
      await this.auth.resetPassword(this.userId, this.token, this.next);
      this.done.set(true);
    } catch (err) {
      this.error.set(serverMessage(err, 'Nie udało się ustawić hasła. Poproś o nowy link.'));
    } finally {
      this.isSaving.set(false);
    }
  }
}
