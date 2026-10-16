import { HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, from, switchMap, throwError } from 'rxjs';

interface ServerError {
  error?: { title?: string; message?: string; errors?: Record<string, string[]> };
}

/**
 * Komunikat z odpowiedzi serwera, a gdy go brak – podany tekst zastępczy.
 * Kolejność: pierwszy błąd walidacji (pola formularza), „message” (np. logowanie), „title” (ProblemDetails).
 */
export function serverMessage(err: unknown, fallback: string): string {
  const body = (err as ServerError)?.error;
  const firstValidation = body?.errors ? Object.values(body.errors).flat()[0] : undefined;
  return firstValidation ?? body?.message ?? body?.title ?? fallback;
}

/**
 * Przy pobieraniu plików (responseType: 'blob') treść błędu przychodzi jako Blob – zamienia ją z powrotem na JSON,
 * żeby {@link serverMessage} pokazał komunikat serwera (np. „Wzór jest wyłączony”).
 */
export function readBlobErrors<T>(): (source: Observable<T>) => Observable<T> {
  return source => source.pipe(catchError((err: unknown) => {
    if (!(err instanceof HttpErrorResponse) || !(err.error instanceof Blob)) return throwError(() => err);
    return from(blobText(err.error)).pipe(switchMap(text => {
      let parsed: unknown = null;
      try {
        parsed = JSON.parse(text);
      } catch {
        parsed = null;
      }
      return throwError(() => new HttpErrorResponse({ error: parsed, headers: err.headers, status: err.status, statusText: err.statusText, url: err.url ?? undefined }));
    }));
  }));
}

function blobText(blob: Blob): Promise<string> {
  if (typeof blob.text === 'function') return blob.text();
  return new Promise(resolve => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.onerror = () => resolve('');
    reader.readAsText(blob);
  });
}
