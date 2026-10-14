import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface Subscription {
  firstName: string;
  optedOut: boolean;
}

/** Strona z linku „wypisz się” w stopce kampanii (bez logowania). Wypisanie wymaga kliknięcia – skanery poczty same nie wypiszą. */
@Component({
  selector: 'app-unsubscribe',
  standalone: true,
  template: `
    <main class="unsub-page">
      <div class="card unsub-card">
        <h2>Wiadomości z DOK Portal</h2>
        @if (state() === 'loading') {
          <p>Wczytywanie…</p>
        } @else if (state() === 'missing') {
          <p class="form-error" role="alert">Ten link jest nieprawidłowy. Skopiuj go z wiadomości w całości albo napisz do Diecezjalnego Ośrodka Katechumenalnego.</p>
        } @else if (subscription(); as s) {
          @if (s.optedOut) {
            <p role="status">{{ s.firstName }}, nie będziesz już dostawać wiadomości z mailingu DOK Portal.
              Ważne sprawy (np. Twoja formacja czy misja) nadal mogą przychodzić osobno.</p>
            <button class="btn ghost" type="button" [disabled]="busy()" (click)="set(false)">To pomyłka – chcę dalej dostawać wiadomości</button>
          } @else {
            <p>{{ s.firstName }}, czy na pewno nie chcesz dostawać wiadomości z mailingu DOK Portal (zaproszenia, informacje dla grup)?</p>
            <button class="btn primary" type="button" [disabled]="busy()" (click)="set(true)">Wypisz mnie</button>
          }
        }
        @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
      </div>
    </main>
  `,
  styles: [`
    .unsub-page { min-height: 100vh; display: grid; place-items: center; padding: 16px; background: var(--bg, #f5f6f8); }
    .unsub-card { width: 100%; max-width: 460px; display: grid; gap: 14px; padding: 24px; font-size: 14px; line-height: 1.5; }
    .form-error { color: var(--danger); }
    h2, p { margin: 0; }
  `]
})
export class UnsubscribeComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly token = inject(ActivatedRoute).snapshot.paramMap.get('token') ?? '';
  readonly state = signal<'loading' | 'ready' | 'missing'>('loading');
  readonly subscription = signal<Subscription | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  private get url(): string {
    return `${environment.apiBaseUrl}/api/mailing/subscription/${encodeURIComponent(this.token)}`;
  }

  async ngOnInit(): Promise<void> {
    try {
      this.subscription.set(await firstValueFrom(this.http.get<Subscription>(this.url)));
      this.state.set('ready');
    } catch {
      this.state.set('missing');
    }
  }

  async set(optOut: boolean): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.subscription.set(await firstValueFrom(this.http.post<Subscription>(`${this.url}/${optOut ? 'unsubscribe' : 'resubscribe'}`, {})));
    } catch {
      this.error.set('Nie udało się zapisać zmiany. Spróbuj ponownie za chwilę.');
    } finally {
      this.busy.set(false);
    }
  }
}
