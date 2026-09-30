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
  readonly showPassword = signal(false);

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly title: Title
  ) {
    this.title.setTitle('Logowanie | Diecezjalny Ośrodek Katechumenalny');
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
    try {
      await this.auth.login(this.email, this.password);
      await this.router.navigateByUrl('/dashboard');
    } catch {
      this.errorMessage.set('Nieprawidłowy e-mail lub hasło.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
