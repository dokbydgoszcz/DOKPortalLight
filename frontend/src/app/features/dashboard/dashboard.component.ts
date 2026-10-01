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
    { date: '2026-10-01', text: 'Czytelne komunikaty błędów zamiast technicznych awarii serwera.' },
    { date: '2026-10-01', text: 'Walidacja formularzy logowania, użytkowników i osób (np. format e-maila, długość hasła).' },
    { date: '2026-10-01', text: 'Blokada konta na 15 minut po 5 nieudanych próbach logowania.' },
    { date: '2026-10-01', text: 'Przycisk „Usuń" na listach (Osoby, Kandydaci, Misje, Formatorzy, Superwizje, Spotkania, Sprawy DOK, Potrzeby parafialne, Budżet) — dane są ukrywane, nie kasowane trwale, więc pomyłkę da się cofnąć.' },
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
