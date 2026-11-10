import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  /** Napis na przycisku potwierdzenia; domyślnie dobierany z pytania („Usunąć…” → „Usuń”). */
  confirmLabel?: string;
  /** Napis na drugim przycisku (domyślnie „Anuluj”) – gdy rezygnacja też coś robi. */
  cancelLabel?: string;
  /** Czerwony przycisk i fokus na rezygnacji; domyślnie wynika z pytania („Usunąć…”, „Wyłączyć…”). */
  danger?: boolean;
  /** Wywoływane, gdy użytkownik zrezygnuje. */
  onCancel?: () => void;
}

export interface PendingConfirm {
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  /** Działanie nieodwracalne lub ograniczające dostęp – czerwony przycisk, fokus na „Anuluj”. */
  danger: boolean;
}

/** Czasownik otwierający pytanie → napis na przycisku i to, czy działanie jest „groźne”. */
const VERBS: ReadonlyArray<[RegExp, string, boolean]> = [
  [/^Usunąć/, 'Usuń', true],
  [/^Wyłączyć/, 'Wyłącz', true],
  [/^Odpiąć/, 'Odepnij', true],
  [/^Cofnąć/, 'Cofnij', false],
  [/^Udostępnić/, 'Udostępnij', false],
  [/^Wznowić/, 'Wznów', false],
  [/^Utworzyć/, 'Utwórz', false],
  [/^Przenieść/, 'Przenieś', false]
];

/**
 * Pytanie „czy na pewno?” w oknie portalu (zamiast systemowego okienka przeglądarki).
 * Okno rysuje ConfirmDialogComponent osadzony w korzeniu aplikacji; gdy go nie ma (np. test pojedynczego
 * ekranu), pytanie zadaje przeglądarka – użytkownik zawsze zostanie zapytany.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly pending = signal<PendingConfirm | null>(null);
  private hosts = 0;
  private onConfirm: (() => void) | null = null;
  private onCancel: (() => void) | null = null;

  /** Okno potwierdzeń zgłasza, że jest na stronie (i wyrejestrowuje się przy zniszczeniu). */
  registerHost(): () => void {
    this.hosts++;
    return () => {
      this.hosts--;
    };
  }

  ask(message: string, onConfirm: () => void, options: ConfirmOptions = {}): void {
    if (this.hosts === 0) {
      if (window.confirm(message)) onConfirm();
      else options.onCancel?.();
      return;
    }
    // Nowe pytanie zastępuje poprzednie – tamto liczy się jako anulowane.
    if (this.pending()) this.answer(false);
    const verb = VERBS.find(([pattern]) => pattern.test(message));
    this.onConfirm = onConfirm;
    this.onCancel = options.onCancel ?? null;
    this.pending.set({
      message,
      confirmLabel: options.confirmLabel ?? verb?.[1] ?? 'Tak',
      cancelLabel: options.cancelLabel ?? 'Anuluj',
      danger: options.danger ?? verb?.[2] ?? false
    });
  }

  answer(confirmed: boolean): void {
    if (!this.pending()) return;
    const action = confirmed ? this.onConfirm : this.onCancel;
    this.onConfirm = null;
    this.onCancel = null;
    this.pending.set(null);
    action?.();
  }
}
