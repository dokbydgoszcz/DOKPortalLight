import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { ToastService } from '../notifications/toast.service';

/**
 * Dokleja token do żądań. Gdy serwer odrzuci sesję (401: token wygasł, konto wyłączone, zmienione role lub hasło),
 * wylogowuje z wyjaśnieniem zamiast zostawiać użytkownika w aplikacji, która „nic nie wczytuje”.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toast = inject(ToastService);
  const token = auth.token;

  if (!token) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
    catchError((error: unknown) => {
      // Kilka równoległych żądań może dostać 401 – wylogowujemy i pokazujemy dymek tylko za pierwszym razem.
      if (error instanceof HttpErrorResponse && error.status === 401 && auth.isAuthenticated()) {
        toast.error('Sesja wygasła – zaloguj się ponownie.');
        auth.logout('expired');
      }
      return throwError(() => error);
    })
  );
};
