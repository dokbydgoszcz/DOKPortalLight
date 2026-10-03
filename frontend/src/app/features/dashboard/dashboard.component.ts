import { Component, OnInit, signal } from '@angular/core';
import { DashboardService } from './dashboard.service';
import { DashboardSummary, DokStageCount } from './dashboard.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly summary = signal<DashboardSummary | null>(null);

  readonly stageLabels: Record<DokStageCount['stage'], string> = {
    Application: 'Zgłoszenie',
    Formation: 'Formacja',
    Sacrament: 'Sakrament',
    Graduate: 'Absolwent'
  };

  readonly changelog: ReadonlyArray<{ date: string; text: string }> = [
    { date: '2026-10-03', text: 'Osoby — adres e-mail musi być unikalny w całym portalu (bywa loginem do aplikacji): nie da się dodać ani zmienić osoby na e-mail, który ma już inna osoba lub konto innej osoby. Powtórzony numer telefonu tylko ostrzega (np. rodzina pod jednym numerem) — portal pokazuje, kto go ma, i pyta, czy zapisać mimo to. Wcześniej wpisane duplikaty zostają, ale można je poprawić lub usunąć.' },
    { date: '2026-10-03', text: 'Misje — w formularzu misji pole „Parafia / miejsce posługi” podpowiada parafie z rejestru po wpisaniu kilku liter (nadal można wpisać dowolne miejsce), a nowy checkbox „Posłany do DOK” zaznacza katechistów posłanych do DOK. Na liście misji i w eksporcie do Excela jest nowa kolumna „Posłany do DOK”.' },
    { date: '2026-10-03', text: 'Naprawiono zapis superwizji, spotkań, misji i operacji budżetowych bez daty — przycisk „Zapisz” jest teraz nieaktywny, dopóki nie wybierzesz wymaganej daty (wcześniej zapis kończył się błędem bez wyjaśnienia). Wyczyszczona data udzielenia misji też nie powoduje już błędu.' },
    { date: '2026-10-03', text: 'Obecność na spotkaniach — na liście spotkań przy każdym spotkaniu są przyciski „Obecny” i „Nieobecny” (ponowne kliknięcie czyści wybór). Zajęcia grupowe mają teraz listę uczestników: w formularzu spotkania zaznaczasz „Zajęcia z listą uczestników” i wybierasz podopiecznych, a obecność ustawiasz osobno dla każdego (rozwijana lista „Uczestnicy”).' },
    { date: '2026-10-03', text: 'Frekwencja podopiecznych — na liście Podopiecznych DOK nowa kolumna pokazuje, na ilu spotkaniach z zapisaną obecnością podopieczny był obecny (np. 4/5 (80%)). Liczą się spotkania indywidualne i zajęcia grupowe.' },
    { date: '2026-10-03', text: 'Katechista widzi tylko swoich podopiecznych — sprawy DOK, ich dokumenty, notatki i spotkania (oraz liczniki na pulpicie) są ograniczone do podopiecznych przypisanych do danego katechisty. Żeby to działało, administrator musi powiązać konto katechisty z jego osobą w ekranie Użytkownicy; konto bez powiązania nie widzi żadnych spraw. Pozostałe role (Administrator, Biskup, Dyrektor DOK, Superwizor) nadal widzą wszystko, a w „Uprawnieniach ról” jest nowa pozycja „Podgląd wszystkich spraw DOK”.' },
    { date: '2026-10-03', text: 'Absolwenci — lista pokazuje teraz wszystkich absolwentów, a nie tylko tych z pierwszych 20 spraw DOK. Doszła wyszukiwarka po imieniu i nazwisku oraz stronicowanie, a data ukończenia wyświetla się czytelnie (dd.mm.rrrr). Ekran działa też dla ról, które mają uprawnienie „Absolwenci”, ale nie widzą listy spraw DOK.' },
    { date: '2026-10-03', text: 'Naprawiono dwa drobne błędy wykryte przy rozbudowie testów: eksport dziennika audytu do CSV nie rozjeżdża już kolumn, gdy opis zawiera przecinki lub cudzysłowy, a na ekranie Dokumenty pojawia się komunikat, gdy generowanie PDF się nie powiedzie.' },
    { date: '2026-10-03', text: 'Nowy ekran „Uprawnienia ról” (tylko Administrator): tabela, w której zaznaczasz, co może każda rola. Można też dodawać własne role (np. „Sekretariat”) i usuwać te, których nikt nie używa. Zmiany działają od razu na serwerze, a w menu i przyciskach użytkownik zobaczy je po ponownym zalogowaniu.' },
    { date: '2026-10-02', text: 'Uprawnienia — menu, ekrany i przyciski (Dodaj, Edytuj, Usuń, Eksport) pokazują się teraz zależnie od uprawnień przypisanych Twojej roli. Po wdrożeniu trzeba zalogować się ponownie. Zaostrzono też dostęp do odczytu: np. budżety, kandydaci SKŚP czy sprawy DOK widzą tylko role, które ich potrzebują.' },
    { date: '2026-10-02', text: 'Eksport do Excela — na listach (Osoby, Podopieczni DOK, Kandydaci SKŚP, Katechiści posłani, Formatorzy, Superwizje, Spotkania, Parafie) pojawił się przycisk „Eksportuj do Excela". Widzą go tylko osoby z odpowiednimi uprawnieniami, a każdy eksport zapisuje się w dzienniku audytu.' },
    { date: '2026-10-02', text: 'Pulpit startowy pokazuje teraz więcej informacji: liczbę podopiecznych DOK w każdym etapie, sprawy z brakującymi dokumentami, spotkania w najbliższych 7 dniach oraz liczbę kandydatów SKŚP.' },
    { date: '2026-10-02', text: 'Nawigacja na telefonie — dodano przycisk menu (☰) w pasku górnym. Wcześniej na wąskich ekranach nie dało się otworzyć menu bocznego.' },
    { date: '2026-10-02', text: 'Naprawiono błąd w formularzach (Formatorzy, Kandydaci, Misje, Podopieczni DOK): przycisk „Zapisz" jest teraz zablokowany, dopóki nie wybierzesz osoby z listy, zamiast zgłaszać błąd dopiero po kliknięciu.' },
    { date: '2026-10-02', text: 'Automatyczne przypomnienia o imieninach — raz w tygodniu każdy użytkownik portalu dostaje e-mail z listą osób mających imieniny w najbliższych 7 dniach.' },
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
