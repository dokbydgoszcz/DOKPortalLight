import { describe, expect, it } from 'vitest';
import { serverMessage } from './http-error';

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
});
