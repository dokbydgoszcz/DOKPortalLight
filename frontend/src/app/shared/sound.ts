/** Krótki dźwięk w odpowiedzi na kliknięcie (np. usunięcie). Cichy brak, gdy przeglądarka nie pozwala odtworzyć. */
export function playSound(src: string): void {
  try {
    const audio = new Audio(src);
    void audio.play()?.catch(() => undefined);
  } catch {
    // Brak dźwięku to nie błąd.
  }
}

export const SOUNDS = {
  deleteResource: '/sounds/usun-zasob.mp3'
} as const;
