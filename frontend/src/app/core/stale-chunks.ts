import { NavigationError } from '@angular/router';

/**
 * Po wdrożeniu nowej wersji pliki ekranów mają nowe nazwy, a stare znikają z serwera.
 * Karta otwarta przed aktualizacją nie wczyta wtedy kolejnego ekranu (np. logowania po „Wyloguj”).
 */
export function isStaleChunkError(error: unknown): boolean {
  const message = String((error as { message?: string })?.message ?? error ?? '');
  return /dynamically imported module|Importing a module script failed|ChunkLoadError|Loading chunk .* failed/i.test(message);
}

/** Zamiast cicho stać w miejscu – pełne przeładowanie pod docelowy adres, już z nową wersją aplikacji. */
export function reloadOnStaleChunk(event: NavigationError, assign: (url: string) => void = url => window.location.assign(url)): void {
  if (isStaleChunkError(event.error)) assign(event.url);
}
