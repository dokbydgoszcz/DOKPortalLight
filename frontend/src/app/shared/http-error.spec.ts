import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError, firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { serverMessage } from './http-error';
import { readBlobErrors } from './http-error';

describe('serverMessage', () => {
  it('prefers the first validation error over the generic title', () => {
    const err = { error: { title: 'One or more validation errors occurred.', errors: { NewPassword: ['Nowe hasło musi mieć co najmniej 8 znaków.'] } } };
    expect(serverMessage(err, 'x')).toBe('Nowe hasło musi mieć co najmniej 8 znaków.');
  });

  it('uses message, then title, then the fallback', () => {
    expect(serverMessage({ error: { message: 'Konto jest nieaktywne.' } }, 'x')).toBe('Konto jest nieaktywne.');
    expect(serverMessage({ error: { title: 'Obecne hasło jest nieprawidłowe.' } }, 'x')).toBe('Obecne hasło jest nieprawidłowe.');
    expect(serverMessage(new Error('boom'), 'Nie udało się.')).toBe('Nie udało się.');
  });

  describe('readBlobErrors', () => {
    it('turns a JSON error blob back into an object, and leaves other errors alone', async () => {
      const blobError = new HttpErrorResponse({ error: new Blob([JSON.stringify({ title: 'Wzór jest wyłączony.' })]), status: 400 });
      const caught = await firstValueFrom(throwError(() => blobError).pipe(readBlobErrors())).catch(e => e);
      expect(serverMessage(caught, 'x')).toBe('Wzór jest wyłączony.');

      const notJson = await firstValueFrom(throwError(() => new HttpErrorResponse({ error: new Blob(['<html>']), status: 502 })).pipe(readBlobErrors())).catch(e => e);
      expect(serverMessage(notJson, 'zastępczy')).toBe('zastępczy');

      const plain = new Error('inny');
      expect(await firstValueFrom(throwError(() => plain).pipe(readBlobErrors())).catch(e => e)).toBe(plain);
      expect(await firstValueFrom(of(1).pipe(readBlobErrors()))).toBe(1);
    });

    it('reads blobs without text() through FileReader', async () => {
      const blob = new Blob([JSON.stringify({ title: 'Stary przeglądarka' })]);
      Object.defineProperty(blob, 'text', { value: undefined });
      const caught = await firstValueFrom(throwError(() => new HttpErrorResponse({ error: blob, status: 400 })).pipe(readBlobErrors())).catch(e => e);
      expect(serverMessage(caught, 'x')).toBe('Stary przeglądarka');
    });
  });
});
