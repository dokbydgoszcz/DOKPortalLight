import { Component, OnInit, signal } from '@angular/core';
import { DashboardService } from './dashboard.service';
import { DashboardSummary } from './dashboard.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly summary = signal<DashboardSummary | null>(null);

  readonly changelog: ReadonlyArray<{ date: string; text: string }> = [
    { date: '2026-10-02', text: 'Automatyczne przypomnienia o spotkaniach — katechista prowadzący dostaje e-mail dzień przed spotkaniem przypisanym do jego sprawy DOK.' },
    { date: '2026-10-02', text: 'Harmonogram spotkań — formularz dodawania/edycji spotkania pozwala teraz wybrać konkretnego podopiecznego DOK, zamiast tylko opisu grupy.' },
    { date: '2026-10-01', text: 'Automatyczne przypomnienia o brakujących dokumentach — raz w tygodniu katechista i Dyrektor DOK dostają e-mail o niedostarczonych dokumentach w swoich sprawach DOK.' },
    { date: '2026-10-01', text: 'Mailing wysyła teraz prawdziwe e-maile do wybranej grupy odbiorców (po skonfigurowaniu serwera SMTP przez administratora).' },
    { date: '2026-10-01', text: 'Dokumenty podopiecznego DOK — nowy przycisk „Dokumenty" pozwala przesłać prawdziwy plik (np. skan metryki) do każdej pozycji na liście i pobrać go później.' },
    { date: '2026-10-01', text: 'Notatki duszpasterskie — nowy przycisk „Notatki" przy podopiecznym DOK. Zwykli katechiści widzą tylko swoje notatki, Administrator i Dyrektor DOK widzą wszystkie.' },
    { date: '2026-10-01', text: 'Paginacja na listach Osób, Kandydatów, Katechistów posłanych i Podopiecznych DOK — szybsze wczytywanie przy dużej liczbie rekordów.' },
    { date: '2026-10-01', text: 'Edycja Formatorów, Superwizji i Spotkań (wcześniej można było je tylko dodawać).' },
    { date: '2026-10-01', text: 'Pełny rejestr 150 parafii Diecezji Bydgoskiej — nowy ekran „Rejestr parafii" z możliwością dodawania kolejnych.' },
    { date: '2026-10-01', text: 'Powiadomienia (dymki) o powodzeniu lub błędzie przy zapisie, edycji i usuwaniu — widoczne w prawym dolnym rogu.' },
    { date: '2026-10-01', text: 'Czytelne komunikaty błędów zamiast technicznych awarii serwera.' },
    { date: '2026-10-01', text: 'Walidacja formularzy logowania, użytkowników i osób (np. format e-maila, długość hasła).' },
    { date: '2026-10-01', text: 'Blokada konta na 15 minut po 5 nieudanych próbach logowania.' },
    { date: '2026-10-01', text: 'Przycisk „Usuń" na listach (Osoby, Kandydaci, Misje, Formatorzy, Superwizje, Spotkania, Sprawy DOK, Parafie, Potrzeby parafialne, Budżet SKŚP i DOK) — dane są ukrywane, nie kasowane trwale, więc pomyłkę da się cofnąć.' },
    { date: '2026-10-01', text: 'Logowanie: spinner i informacja o wybudzaniu serwera, gdy backend jest uśpiony.' },
    { date: '2026-10-01', text: 'Automatyczne wylogowanie po 20 minutach bezczynności.' },
    { date: '2026-10-01', text: 'Naprawiono błąd 404 po odświeżeniu strony.' },
    { date: '2026-10-01', text: 'Dodawanie użytkownika: łączenie z istniejącą osobą lub dodanie nowej, potwierdzenie hasła.' },
    { date: '2026-10-01', text: 'Administrator może zresetować hasło użytkownika.' },
    { date: '2026-09-30', text: 'Nowy wygląd strony logowania i całej aplikacji.' }
  ];

  constructor(private readonly dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.getSummary().subscribe(summary => this.summary.set(summary));
  }
}
