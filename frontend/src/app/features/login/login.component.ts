import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { AuthService } from '../../core/auth/auth.service';

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
      await this.auth.login(this.email, this.password);
      await this.router.navigateByUrl('/dashboard');
    } catch {
      this.errorMessage.set('Nieprawidłowy e-mail lub hasło.');
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
