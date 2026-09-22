import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/auth/auth.service';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceMock: { login: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    authServiceMock = { login: vi.fn() };

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: authServiceMock }]
    });

    fixture = TestBed.createComponent(LoginComponent);
  });

  it('navigates to /dashboard after a successful login', async () => {
    authServiceMock.login.mockResolvedValue(undefined);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const component = fixture.componentInstance;
    component.email = 'a@b.pl';
    component.password = 'secret';

    await component.submit();

    expect(authServiceMock.login).toHaveBeenCalledWith('a@b.pl', 'secret');
    expect(navigateSpy).toHaveBeenCalledWith('/dashboard');
  });

  it('shows an error message when login fails', async () => {
    authServiceMock.login.mockRejectedValue(new Error('unauthorized'));
    const component = fixture.componentInstance;

    await component.submit();

    expect(component.errorMessage()).toBe('Nieprawidłowy e-mail lub hasło.');
  });
});
