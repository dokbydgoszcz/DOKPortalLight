import { Component, ElementRef, HostListener, OnDestroy, effect, inject, viewChild } from '@angular/core';
import { ConfirmService } from './confirm.service';

/** Okno „czy na pewno?” – jedno na całą aplikację, sterowane przez ConfirmService. */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  template: `
    @if (confirm.pending(); as question) {
      <div class="overlay show confirm-overlay" (click)="confirm.answer(false)">
        <div class="modal confirm-modal" role="alertdialog" aria-modal="true" aria-labelledby="confirm-message" (click)="$event.stopPropagation()">
          <div class="modal-body">
            <p id="confirm-message" class="confirm-message">{{ question.message }}</p>
          </div>
          <div class="modal-foot">
            <button #cancel class="btn ghost" type="button" (click)="confirm.answer(false)">{{ question.cancelLabel }}</button>
            <button #ok class="btn" type="button" [class.danger-solid]="question.danger" [class.primary]="!question.danger" (click)="confirm.answer(true)">
              {{ question.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .confirm-overlay { z-index: 1000; }
    .confirm-modal { width: min(460px, 94vw); }
    .confirm-message { margin: 0; font-size: 14px; line-height: 1.5; white-space: pre-line; overflow-wrap: anywhere; }
    .danger-solid { background: var(--danger, #cb4455); color: white; }
    .danger-solid:hover { filter: brightness(.94); }
  `]
})
export class ConfirmDialogComponent implements OnDestroy {
  readonly confirm = inject(ConfirmService);
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancel');
  private readonly okButton = viewChild<ElementRef<HTMLButtonElement>>('ok');
  private readonly unregister = this.confirm.registerHost();
  /** Element, który miał fokus przed otwarciem okna – wraca tam po zamknięciu. */
  private opener: HTMLElement | null = null;

  constructor() {
    effect(() => {
      const question = this.confirm.pending();
      const target = question?.danger ? this.cancelButton() : this.okButton();
      if (question && target) {
        this.opener ??= document.activeElement as HTMLElement | null;
        target.nativeElement.focus();
      } else if (!question && this.opener) {
        this.opener.focus?.();
        this.opener = null;
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.confirm.answer(false);
  }

  /** Tab krąży między dwoma przyciskami okna (nie ucieka na stronę pod spodem). */
  @HostListener('document:keydown.tab', ['$event'])
  onTab(event: Event): void {
    if (!this.confirm.pending()) return;
    event.preventDefault();
    const next = document.activeElement === this.okButton()?.nativeElement ? this.cancelButton() : this.okButton();
    next?.nativeElement.focus();
  }

  @HostListener('document:keydown.shift.tab', ['$event'])
  onShiftTab(event: Event): void {
    this.onTab(event);
  }

  ngOnDestroy(): void {
    this.unregister();
  }
}
