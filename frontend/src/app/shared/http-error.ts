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
