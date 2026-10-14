import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi, afterEach } from 'vitest';
import { MfaSettingsComponent } from './mfa-settings.component';
import { AuthService } from '../../core/auth/auth.service';
import { ToastService } from '../../core/notifications/toast.service';

function setup(overrides: Record<string, unknown> = {}) {
  const auth = {
    mfaStatus: vi.fn().mockResolvedValue({ enabled: false, recoveryCodesLeft: 0 }),
    setupMfa: vi.fn().mockResolvedValue({ sharedKey: 'ABCDEFGHIJKLMNOP', otpAuthUri: 'otpauth://totp/DOK%20Portal:a?secret=ABCD' }),
    enableMfa: vi.fn().mockResolvedValue(['AAAAA-BBBBB', 'CCCCC-DDDDD']),
    disableMfa: vi.fn().mockResolvedValue(undefined),
    ...overrides
  };
  TestBed.configureTestingModule({ imports: [MfaSettingsComponent], providers: [{ provide: AuthService, useValue: auth }] });
  const fixture = TestBed.createComponent(MfaSettingsComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, auth, el: fixture.nativeElement as HTMLElement };
}

const toasts = () => TestBed.inject(ToastService).toasts().map(t => t.message);

describe('MfaSettingsComponent', () => {
  afterEach(() => vi.restoreAllMocks());

  it('walks through the setup: QR code, key, code and recovery codes', async () => {
    const { fixture, component, auth, el } = setup();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el.textContent).toContain('Włącz weryfikację dwuetapową');

    await component.start();
    fixture.detectChanges();
    expect(el.querySelector('img.qr')?.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
    expect(el.textContent).toContain('ABCD EFGH IJKL MNOP');

    component.code = '123456';
    auth.mfaStatus.mockResolvedValue({ enabled: true, recoveryCodesLeft: 2 });
    await component.enable();
    fixture.detectChanges();
    expect(auth.enableMfa).toHaveBeenCalledWith('123456');
    expect(el.textContent).toContain('AAAAA-BBBBB');

    component.recoveryCodes.set(null);
    fixture.detectChanges();
    expect(el.textContent).toContain('Pozostałe kody zapasowe: 2');
  });

  it('shows a wrong code during setup and can cancel it', async () => {
    const { component } = setup({ enableMfa: vi.fn().mockRejectedValue({ error: { title: 'Nieprawidłowy kod.' } }) });
    await component.start();
    component.code = '000000';
    await component.enable();
    expect(component.error()).toBe('Nieprawidłowy kod.');
    component.cancelSetup();
    expect(component.setup()).toBeNull();
  });

  it('disables MFA with a code after confirmation', async () => {
    const { fixture, component, auth } = setup({ mfaStatus: vi.fn().mockResolvedValue({ enabled: true, recoveryCodesLeft: 10 }) });
    await fixture.whenStable();
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);
    component.code = '123456';
    await component.disable();
    expect(auth.disableMfa).not.toHaveBeenCalled();
    await component.disable();
    expect(auth.disableMfa).toHaveBeenCalledWith('123456');
    expect(toasts()).toContain('Weryfikacja dwuetapowa wyłączona.');
  });

  it('reports errors of status, setup and disabling', async () => {
    const { fixture, component } = setup({
      mfaStatus: vi.fn().mockRejectedValue(new Error('x')),
      setupMfa: vi.fn().mockRejectedValue(new Error('x')),
      disableMfa: vi.fn().mockRejectedValue({ error: { title: 'Nieprawidłowy kod.' } })
    });
    await fixture.whenStable();
    expect(component.error()).toBe('Nie udało się sprawdzić weryfikacji dwuetapowej.');
    await component.start();
    expect(component.error()).toBe('Nie udało się rozpocząć konfiguracji.');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await component.disable();
    expect(component.error()).toBe('Nieprawidłowy kod.');
  });

  it('copies the recovery codes, or says it could not', async () => {
    const { component } = setup();
    const write = vi.fn().mockResolvedValueOnce(undefined).mockRejectedValueOnce(new Error('no'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: write }, configurable: true });
    await component.copyCodes(['A', 'B']);
    expect(write).toHaveBeenCalledWith('A\nB');
    await component.copyCodes(['A']);
    expect(toasts()).toContain('Skopiowano kody zapasowe.');
    expect(toasts()).toContain('Nie udało się skopiować – przepisz kody ręcznie.');
  });

  it('can be driven entirely with the buttons and fields', async () => {
    const { fixture, auth, el } = setup();
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: vi.fn().mockResolvedValue(undefined) }, configurable: true });
    const settle = async () => { for (let i = 0; i < 5; i++) { await new Promise(r => setTimeout(r, 10)); fixture.detectChanges(); } };
    const button = (text: string) => Array.from(el.querySelectorAll('button')).find(b => b.textContent!.includes(text)) as HTMLButtonElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector(selector) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    await settle();

    button('Włącz weryfikację dwuetapową').click();
    await settle();
    button('Anuluj').click();
    await settle();
    button('Włącz weryfikację dwuetapową').click();
    await settle();
    type('#mfa-setup-code', '123456');
    await settle();
    auth.mfaStatus.mockResolvedValue({ enabled: true, recoveryCodesLeft: 2 });
    button('Włącz weryfikację').click();
    await settle();
    expect(auth.enableMfa).toHaveBeenCalledWith('123456');
    button('Kopiuj kody').click();
    button('Zapisałem kody').click();
    await settle();

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    type('#mfa-disable-code', '654321');
    await settle();
    button('Wyłącz weryfikację dwuetapową').click();
    await settle();
    expect(auth.disableMfa).toHaveBeenCalledWith('654321');
  });
});
