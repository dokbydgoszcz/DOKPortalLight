import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: number;
  message: string;
  kind: 'success' | 'error';
}

const AUTO_DISMISS_MS = 4000;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  readonly toasts = signal<ToastMessage[]>([]);

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  dismiss(id: number): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }

  private show(message: string, kind: ToastMessage['kind']): void {
    const id = ++this.nextId;
    this.toasts.update(list => [...list, { id, message, kind }]);
    setTimeout(() => this.dismiss(id), AUTO_DISMISS_MS);
  }
}
