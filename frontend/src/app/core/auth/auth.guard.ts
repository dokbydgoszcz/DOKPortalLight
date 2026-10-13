import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const CHANGE_PASSWORD_URL = '/konto/haslo';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) return router.parseUrl('/login');
  // Konto z hasłem od administratora: najpierw własne hasło, potem reszta portalu.
  if (auth.mustChangePassword?.() && !state?.url?.startsWith(CHANGE_PASSWORD_URL)) return router.parseUrl(CHANGE_PASSWORD_URL);
  return true;
};
