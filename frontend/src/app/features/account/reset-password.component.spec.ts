import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { ResetPasswordComponent } from './reset-password.component';
import { AuthService } from '../../core/auth/auth.service';

function setup(params: Record<string, string>, resetPassword = vi.fn().mockResolvedValue(undefined)) {
  TestBed.configureTestingModule({
    imports: [ResetPasswordComponent],
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(params) } } },
      { provide: AuthService, useValue: { resetPassword } }
    ]
  });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  const fixture = TestBed.createComponent(ResetPasswordComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, resetPassword, navigate, el: fixture.nativeElement as HTMLElement };
}

describe('ResetPasswordComponent', () => {
  it('takes the code from the link, removes it from the address bar and sets the new password', async () => {
    const { component, resetPassword, navigate, fixture, el } = setup({ uid: 'u1', token: 'kod' });
    expect(navigate).toHaveBeenCalledWith([], { queryParams: {}, replaceUrl: true });

    component.next = component.repeat = 'Nowe12345!';
    await component.submit();
    fixture.detectChanges();

    expect(resetPassword).toHaveBeenCalledWith('u1', 'kod', 'Nowe12345!');
    expect(el.textContent).toContain('Hasło zostało ustawione');
  });

  it('explains an incomplete link', () => {
    const { el, navigate } = setup({});
    expect(el.textContent).toContain('Link jest niepełny');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('checks the passwords before sending and shows the server reason for an expired link', async () => {
    const { component, resetPassword, fixture, el } = setup(
      { uid: 'u1', token: 'kod' },
      vi.fn().mockRejectedValue({ error: { title: 'Link do zmiany hasła jest nieprawidłowy albo wygasł.' } }));
    component.next = 'krótkie';
    expect(component.problem).toContain('8 znaków');
    component.next = 'Nowe12345!';
    component.repeat = 'Inne12345!';
    expect(component.problem).toContain('inne');
    await component.submit();
    expect(resetPassword).not.toHaveBeenCalled();

    component.repeat = 'Nowe12345!';
    await component.submit();
    fixture.detectChanges();
    expect(el.textContent).toContain('wygasł');
  });
  it('submits the passwords typed into the form', async () => {
    const { fixture, el, resetPassword } = setup({ uid: 'u1', token: 'kod' });
    await fixture.whenStable();
    const type = (sel: string, v: string) => {
      const i = el.querySelector(sel) as HTMLInputElement;
      i.value = v;
      i.dispatchEvent(new Event('input'));
    };
    type('#reset-new', 'Nowe12345!');
    type('#reset-repeat', 'Nowe12345!');
    fixture.detectChanges();
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(resetPassword).toHaveBeenCalledWith('u1', 'kod', 'Nowe12345!');
  });
});
