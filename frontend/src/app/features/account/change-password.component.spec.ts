import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';
import { ChangePasswordComponent } from './change-password.component';
import { AuthService } from '../../core/auth/auth.service';

function setup(changePassword = vi.fn().mockResolvedValue(undefined)) {
  TestBed.configureTestingModule({
    imports: [ChangePasswordComponent],
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { email: () => 'kat@example.org', mustChangePassword: signal(true), changePassword } }
    ]
  });
  const fixture = TestBed.createComponent(ChangePasswordComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, changePassword };
}

describe('ChangePasswordComponent', () => {
  it('explains why the change is required for an admin-given password', () => {
    const { fixture } = setup();
    expect(fixture.nativeElement.textContent).toContain('Hasło nadał administrator');
  });

  it('blocks too short, mismatched or unchanged passwords before sending', () => {
    const { component } = setup();
    component.current = 'Stare1234!';
    component.next = 'krot';
    expect(component.problem).toContain('8 znaków');
    component.next = 'Nowe12345!';
    component.repeat = 'Inne12345!';
    expect(component.problem).toContain('inne');
    component.next = component.repeat = 'Stare1234!';
    expect(component.problem).toContain('różnić');
    expect(component.canSubmit).toBe(false);
  });

  it('changes the password and goes to the dashboard', async () => {
    const { component, changePassword } = setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    component.current = 'Stare1234!';
    component.next = component.repeat = 'Nowe12345!';

    await component.submit();

    expect(changePassword).toHaveBeenCalledWith('Stare1234!', 'Nowe12345!');
    expect(navigate).toHaveBeenCalledWith('/dashboard');
  });

  it('shows the server message when the current password is wrong', async () => {
    const { component } = setup(vi.fn().mockRejectedValue({ error: { title: 'Obecne hasło jest nieprawidłowe.' } }));
    component.current = 'zle';
    component.next = component.repeat = 'Nowe12345!';

    await component.submit();

    expect(component.error()).toBe('Obecne hasło jest nieprawidłowe.');
  });
});
