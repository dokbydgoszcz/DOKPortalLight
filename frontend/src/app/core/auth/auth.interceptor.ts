import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Dokleja token do żądań. Gdy serwer odrzuci sesję (401: token wygasł, konto wyłączone, zmienione role lub hasło),
 * wylogowuje z wyjaśnieniem zamiast zostawiać użytkownika w aplikacji, która „nic nie wczytuje”.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;

  if (!token) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        auth.logout('expired');
      }
      return throwError(() => error);
    })
  );
};
