import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Instrukcja: jak dodać spotkania z portalu do Kalendarza Google w telefonie (i na iPhonie). */
@Component({
  selector: 'app-calendar-howto',
  standalone: true,
  imports: [RouterLink],
  template: `
    <ol class="steps">
      <li>
        @if (linkToAccount()) {
          Otwórz <a routerLink="/konto/haslo">Moje konto</a> (przycisk u góry ekranu), w sekcji „Kalendarz w telefonie” kliknij
        } @else {
          Kliknij
        }
        <b>„Utwórz adres kalendarza”</b>, potem <b>„Kopiuj”</b>.
      </li>
      <li>Na <b>komputerze</b> otwórz <b>calendar.google.com</b>, przy „Inne kalendarze” wybierz <b>＋</b> → <b>„Z adresu URL”</b>,
        wklej adres i kliknij „Dodaj kalendarz”. Ta opcja jest tylko na komputerze – w aplikacji na telefonie jej nie ma.</li>
      <li>Kalendarz <b>„DOK Portal – spotkania”</b> pojawi się w aplikacji Kalendarz Google na telefonie. Jeśli go nie widać:
        w aplikacji Ustawienia → wybierz ten kalendarz → włącz „Synchronizacja”.</li>
    </ol>
    <p class="note">Google odświeża taki kalendarz sam, zwykle co kilka godzin – nowe lub zmienione spotkanie może pojawić się w telefonie z opóźnieniem.
      <b>iPhone:</b> Ustawienia → Kalendarz → Konta → Dodaj konto → Inne → „Dodaj subskrybowany kalendarz” i wklej ten sam adres.</p>
  `,
  styles: [`
    :host { display: block; font-size: 13px; line-height: 1.55; }
    .steps { margin: 0 0 8px; padding-left: 20px; display: grid; gap: 6px; }
    .note { margin: 0; color: var(--muted); }
  `]
})
export class CalendarHowtoComponent {
  /** Pierwszy krok z linkiem do „Moje konto” (gdy instrukcja jest poza tą stroną). */
  readonly linkToAccount = input(false);
}
