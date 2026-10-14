import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { AuthService } from '../../core/auth/auth.service';
import { serverMessage } from '../../shared/http-error';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  email = '';
  password = '';
  readonly errorMessage = signal<string | null>(null);
  readonly isSubmitting = signal(false);
  readonly isWakingUp = signal(false);
  readonly showPassword = signal(false);
  readonly sessionExpiredMessage = signal<string | null>(null);
  /** „Nie pamiętam hasła”: formularz prośby o link zamiast logowania. */
  readonly isForgotMode = signal(false);
  readonly forgotMessage = signal<string | null>(null);
  /** Drugi krok logowania (konto z MFA): bilet z pierwszego kroku i kod z aplikacji. */
  readonly mfaToken = signal<string | null>(null);
  mfaCode = '';

  private wakeUpTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly title: Title
  ) {
    this.title.setTitle('Logowanie | Diecezjalny Ośrodek Katechumenalny');
    const navigationState = this.router.getCurrentNavigation()?.extras?.state as { reason?: string } | undefined;
    const historyState = history.state as { reason?: string } | null;
    const reason = navigationState?.reason ?? historyState?.reason;
    if (reason === 'idle') {
      this.sessionExpiredMessage.set('Sesja wygasła z powodu braku aktywności. Zaloguj się ponownie.');
    } else if (reason === 'expired') {
      this.sessionExpiredMessage.set('Sesja wygasła lub konto zostało zmienione (np. role). Zaloguj się ponownie.');
    }
  }

  async submitMfa(): Promise<void> {
    const token = this.mfaToken();
    if (!token || this.isSubmitting() || !this.mfaCode.trim()) return;
    this.errorMessage.set(null);
    this.isSubmitting.set(true);
    try {
      await this.auth.verifyMfa(token, this.mfaCode.trim());
      await this.router.navigateByUrl('/dashboard');
    } catch (err) {
      this.errorMessage.set(serverMessage(err, 'Nieprawidłowy kod.'));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  cancelMfa(): void {
    this.mfaToken.set(null);
    this.mfaCode = '';
    this.password = '';
    this.errorMessage.set(null);
  }

  showForgot(show: boolean): void {
    this.isForgotMode.set(show);
    this.errorMessage.set(null);
    this.forgotMessage.set(null);
  }

  async sendResetLink(): Promise<void> {
    if (this.isSubmitting() || !this.email.trim()) return;
    this.errorMessage.set(null);
    this.isSubmitting.set(true);
    try {
      this.forgotMessage.set(await this.auth.forgotPassword(this.email.trim()));
    } catch (err) {
      this.errorMessage.set(serverMessage(err, 'Nie udało się wysłać linku. Spróbuj za chwilę.'));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(value => !value);
  }

  async submit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }
    this.errorMessage.set(null);
    this.isSubmitting.set(true);
    this.isWakingUp.set(false);
    this.wakeUpTimer = setTimeout(() => this.isWakingUp.set(true), 4000);
    try {
      const result = await this.auth.login(this.email, this.password);
      if (result?.mfaToken) {
        this.mfaCode = '';
        this.mfaToken.set(result.mfaToken);
        return;
      }
      await this.router.navigateByUrl('/dashboard');
    } catch (err) {
      this.errorMessage.set(serverMessage(err, 'Nieprawidłowy e-mail lub hasło.'));
    } finally {
      if (this.wakeUpTimer) {
        clearTimeout(this.wakeUpTimer);
        this.wakeUpTimer = null;
      }
      this.isSubmitting.set(false);
      this.isWakingUp.set(false);
    }
  }
}
