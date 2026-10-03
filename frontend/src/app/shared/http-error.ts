/** Komunikat z odpowiedzi serwera (ProblemDetails.title), a gdy go brak – podany tekst zastępczy. */
export function serverMessage(err: unknown, fallback: string): string {
  return (err as { error?: { title?: string } })?.error?.title ?? fallback;
}
