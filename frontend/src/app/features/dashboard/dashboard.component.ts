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
    { date: '2026-10-03', text: 'Naprawiono Mailing — kampanii nie dało się zapisać bez tematu i treści, a mimo to się zapisywała. Teraz formularz „Nowa kampania” oznacza pola obowiązkowe (*), wyłącza „Zapisz”, dopóki temat i treść nie mają tekstu, i ogranicza temat do 200 znaków; to samo sprawdza serwer.' },
    { date: '2026-10-03', text: 'Dolny pasek — na każdym ekranie po zalogowaniu widać stopkę z prawami autorskimi (Diecezja Bydgoska, Diecezjalny Ośrodek Katechumenalny) oraz nazwą i numerem wersji aplikacji (DOK Portal · wersja 1.0.0).' },
    { date: '2026-10-03', text: 'Kandydaci SKŚP — przy kandydacie jest „Edytuj” (osoba, rok, frekwencja, opinie, rekolekcje). Rekolekcje są teraz osobne dla każdego roku formacji (I, II, III rok): w formularzu wybierasz dla każdego roku „—”, „oczekuje” lub „zaliczone”, a w tabeli widać status każdego roku. Dotychczasowe „zaliczone” zostało przeniesione do rekolekcji bieżącego roku kandydata. W eksporcie do Excela kolumna „Rekolekcje” pokazuje zaliczone lata (np. „I, II”).' },
    { date: '2026-10-03', text: 'Parafie i giełda posługi — zapotrzebowanie można edytować („Edytuj”: parafia i opis), także po skierowaniu katechisty. Do jednego zapotrzebowania można skierować wielu katechistów („＋ Kolejna osoba”), a każdego można odpiąć krzyżykiem przy nazwisku; gdy odepniesz ostatniego, zapotrzebowanie wraca do statusu otwartego. Formularze nie pozwalają zapisać pustej parafii, opisu ani skierowania bez wybranej osoby.' },
    { date: '2026-10-03', text: 'Pliki w notatkach duszpasterskich — przy każdej notatce można dołączyć pliki (np. eksport z Kindle Scribe): „＋ Dodaj plik”, a przy pisaniu nowej notatki „＋ Dołącz pliki”. Notatka może składać się wyłącznie z plików — dostanie wtedy opis z ich nazw. Pliki widzi i pobiera ten, kto widzi notatkę (autor lub osoba z dostępem do wszystkich notatek); każde pobranie trafia do dziennika audytu.' },
    { date: '2026-10-03', text: 'Pliki w superwizjach — w wierszu superwizji jest „Załączniki” (z liczbą plików): okno z listą, pobieraniem i usuwaniem; dodawać i usuwać mogą osoby zarządzające superwizjami, pobierać każdy, kto widzi superwizje.' },
    { date: '2026-10-03', text: 'Dokumenty podopiecznych DOK — na górze okna „Dokumenty” jest przycisk „＋ Prześlij pliki”: każdy wybrany plik zakłada pozycję na liście (nazwa z nazwy pliku) i od razu się do niej wysyła, także gdy lista jest jeszcze pusta. Przycisk „Prześlij plik” przy pozycji zostaje.' },
    { date: '2026-10-03', text: 'Pliki w całym portalu — dozwolone typy to PDF, JPG/JPEG, PNG, DOCX, DOC i TXT, do 20 MB na plik. Dotyczy też dokumentów podopiecznych DOK (inne formaty, np. Excel, nie są już przyjmowane). Niedozwolony lub za duży plik jest odrzucany z jasnym komunikatem, a przy wielu plikach reszta i tak się wysyła.' },
    { date: '2026-10-03', text: 'Audit log — nad tabelą są teraz wyszukiwarka (użytkownik, akcja lub obiekt) oraz filtry: akcja, wynik (dozwolono / zablokowano) i zakres dat. Data jest czytelna (dd.mm.rrrr gg:mm:ss), a „Wyczyść filtry” wraca do pełnej listy. Wyświetlane jest do 500 najnowszych wpisów — zawęź filtry, aby zobaczyć starsze.' },
    { date: '2026-10-03', text: 'Superwizje — nad listą są filtr po instytucji (SKŚP / DOK) i sortowanie: po dacie (najnowsze lub najstarsze) albo po instytucji, a w niej po dacie. Data wyświetla się jako dd.mm.rrrr.' },
    { date: '2026-10-03', text: 'Harmonogram i obecności — w formularzu spotkania jest pole „Notatki” (przy dodawaniu i edycji); notatki widać w tabeli spotkań.' },
    { date: '2026-10-03', text: 'Mailing — szkic kampanii można usunąć („Usuń” przy szkicu, z potwierdzeniem). Wysłanych kampanii nie da się usunąć, zostają w historii.' },
    { date: '2026-10-03', text: 'Parafie — w rejestrze parafii jest „Edytuj” (nazwa i miejscowość). Przyciski dodawania, edycji i usuwania widzą tylko osoby z uprawnieniem do zarządzania parafiami.' },
    { date: '2026-10-03', text: 'Dokumenty i pisma — w historii wygenerowanych pism jest teraz „Pobierz” (ponowne pobranie tego samego pliku PDF) i „Usuń” (z potwierdzeniem; tylko dla osób, które mogą generować pisma). Historia pokazuje czytelną datę (dd.mm.rrrr gg:mm) oraz liczbę pobrań każdego pisma. Od teraz egzemplarz PDF jest zapisywany; przy starszych pismach, wygenerowanych wcześniej, pobranie odtwarza je z aktualnych danych osoby (z oznaczeniem).' },
    { date: '2026-10-03', text: 'Misje — w wierszu misji jest teraz „Edytuj”: można poprawić miejsce posługi, daty, grupę superwizyjną i zaznaczenie „Posłany do DOK” bez usuwania i dodawania misji od nowa.' },
    { date: '2026-10-03', text: 'Użytkownicy — w tabeli kont jest nowa kolumna „Osoba”. Istniejące konto można powiązać z osobą („Powiąż”), zmienić tę osobę („Zmień”) albo odpiąć („Odepnij”). Jedna osoba może mieć jedno konto. Katechista widzi podopiecznych dopiero po powiązaniu jego konta z jego osobą; konta katechistów bez osoby są oznaczone ostrzeżeniem.' },
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
