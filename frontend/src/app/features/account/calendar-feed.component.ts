import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../core/notifications/toast.service';
import { serverMessage } from '../../shared/http-error';

/** Prywatny adres kalendarza (iCal): spotkania z harmonogramu w Google Calendar / Outlooku / iPhonie. */
@Component({
  selector: 'app-calendar-feed',
  standalone: true,
  template: `
    <section class="card">
      <div class="card-head">
        <h3>Kalendarz w telefonie</h3>
        @if (url()) { <span class="pill green">włączony</span> }
      </div>
      <div class="card-body feed-body">
        <p>Twoje spotkania z „Harmonogramu i obecności” mogą same pojawiać się w Kalendarzu Google (także w telefonie), w Outlooku albo na iPhonie.</p>
        @if (url(); as address) {
          <div class="address">
            <input readonly [value]="address" aria-label="Prywatny adres kalendarza" (focus)="$any($event.target).select()" />
            <button class="btn primary small" type="button" (click)="copy(address)">Kopiuj</button>
          </div>
          <details>
            <summary>Jak dodać do Kalendarza Google?</summary>
            <ol>
              <li>Na komputerze otwórz <b>calendar.google.com</b> (w aplikacji na telefonie tej opcji nie ma).</li>
              <li>Po lewej przy „Inne kalendarze” kliknij <b>＋</b> → <b>Z adresu URL</b>.</li>
              <li>Wklej skopiowany adres i kliknij <b>Dodaj kalendarz</b>.</li>
              <li>Kalendarz „DOK Portal – spotkania” pojawi się też w aplikacji na telefonie (jeśli go nie widać: w aplikacji Ustawienia → wybierz kalendarz → Synchronizacja).</li>
            </ol>
            <p class="small-muted">Google odświeża takie kalendarze samo, zwykle co kilka godzin – nowe spotkanie może pojawić się z opóźnieniem.
              Na iPhonie: Ustawienia → Kalendarz → Konta → Dodaj konto → Inne → Dodaj subskrybowany kalendarz.</p>
          </details>
          <p class="small-muted">Adres działa jak hasło: kto go zna, widzi Twoje spotkania (imiona i nazwiska podopiecznych). Nie przesyłaj go dalej.
            Gdy wycieknie – utwórz nowy, a stary przestanie działać.</p>
          <div class="row">
            <button class="btn ghost small" type="button" [disabled]="busy()" (click)="regenerate()">Utwórz nowy adres</button>
            <button class="btn ghost small danger" type="button" [disabled]="busy()" (click)="disable()">Wyłącz</button>
          </div>
        } @else {
          <button class="btn primary" type="button" [disabled]="busy() || !loaded()" (click)="create()">Utwórz adres kalendarza</button>
        }
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; }
    .card { margin: 0; }
    .feed-body { display: grid; gap: 12px; font-size: 13px; line-height: 1.5; }
    p { margin: 0; color: var(--muted); }
    .address { display: flex; gap: 8px; }
    .address input { flex: 1; min-width: 0; border: 1px solid var(--line); border-radius: 10px; padding: 8px 10px; font-size: 12px; background: #f7faf8; }
    details summary { cursor: pointer; font-weight: 700; }
    ol { margin: 8px 0; padding-left: 18px; display: grid; gap: 4px; }
    .row { display: flex; gap: 8px; flex-wrap: wrap; }
    .danger { color: var(--danger); }
    button { justify-self: start; }
  `]
})
export class CalendarFeedComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/calendar/feed`;

  readonly url = signal<string | null>(null);
  readonly loaded = signal(false);
  readonly busy = signal(false);

  async ngOnInit(): Promise<void> {
    try {
      this.url.set((await firstValueFrom(this.http.get<{ url: string | null }>(this.baseUrl))).url);
    } catch {
      this.toast.error('Nie udało się sprawdzić adresu kalendarza.');
    } finally {
      this.loaded.set(true);
    }
  }

  private async run(action: () => Promise<void>, failure: string): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (err) {
      this.toast.error(serverMessage(err, failure));
    } finally {
      this.busy.set(false);
    }
  }

  create(): Promise<void> {
    return this.run(async () => {
      this.url.set((await firstValueFrom(this.http.post<{ url: string }>(this.baseUrl, null))).url);
    }, 'Nie udało się utworzyć adresu kalendarza.');
  }

  regenerate(): Promise<void> {
    if (!confirm('Utworzyć nowy adres? Stary przestanie działać – trzeba będzie dodać kalendarz w Google od nowa.')) return Promise.resolve();
    return this.run(async () => {
      this.url.set((await firstValueFrom(this.http.post<{ url: string }>(this.baseUrl, null))).url);
      this.toast.success('Utworzono nowy adres – dodaj go ponownie w Kalendarzu Google.');
    }, 'Nie udało się utworzyć adresu kalendarza.');
  }

  disable(): Promise<void> {
    if (!confirm('Wyłączyć adres kalendarza? Spotkania znikną z Kalendarza Google przy najbliższym odświeżeniu.')) return Promise.resolve();
    return this.run(async () => {
      await firstValueFrom(this.http.delete(this.baseUrl));
      this.url.set(null);
      this.toast.success('Adres kalendarza wyłączony.');
    }, 'Nie udało się wyłączyć adresu kalendarza.');
  }

  async copy(address: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(address);
      this.toast.success('Skopiowano adres kalendarza.');
    } catch {
      this.toast.error('Nie udało się skopiować – zaznacz adres i skopiuj ręcznie.');
    }
  }
}
