export type HelpAudience = 'catechist' | 'director' | 'account';

export interface HelpTopic {
  title: string;
  /** Gdzie to jest w portalu, np. „Podopieczni DOK → Notatki”. */
  where: string;
  /** Adres ekranu w portalu (link „Otwórz”). */
  link?: string;
  steps: string[];
  tip?: string;
}

export const HELP_AUDIENCES: ReadonlyArray<{ id: HelpAudience; label: string; intro: string }> = [
  { id: 'catechist', label: 'Katechista prowadzący', intro: 'Prowadzisz podopiecznych DOK: notatki, dokumenty, spotkania i obecność.' },
  { id: 'director', label: 'Dyrektor', intro: 'Prowadzisz DOK albo Szkołę Katechistów: osoby, sprawy, kandydaci, misje, pisma i mailing.' },
  { id: 'account', label: 'Konto i logowanie', intro: 'Hasło, weryfikacja dwuetapowa i kalendarz w telefonie – dla każdego.' }
];

export const HELP_TOPICS: Readonly<Record<HelpAudience, ReadonlyArray<HelpTopic>>> = {
  catechist: [
    {
      title: 'Moi podopieczni',
      where: 'Menu → Podopieczni DOK',
      link: '/dok-cases',
      steps: [
        'Na liście widzisz tylko osoby, które prowadzisz. Nowych podopiecznych przypisuje Ci dyrektor.',
        'Przy każdej osobie masz przyciski: Notatki, Dokumenty i Historia.',
        'Lista jest pusta, choć masz podopiecznych? Twoje konto nie jest powiązane z Twoją osobą – zgłoś to administratorowi.'
      ]
    },
    {
      title: 'Notatka po spotkaniu',
      where: 'Podopieczni DOK → Notatki',
      link: '/dok-cases',
      steps: [
        'Kliknij „Notatki” przy podopiecznym.',
        'Wpisz treść i kliknij „Dodaj notatkę”. Możesz dołączyć pliki („Dołącz pliki”), np. skan odręcznych notatek.',
        'Własną notatkę możesz potem poprawić albo usunąć.'
      ],
      tip: 'Notatki duszpasterskie widzisz tylko Ty (i osoby z osobnym uprawnieniem nadzoru). Inny katechista ich nie zobaczy, dopóki sam ich nie udostępnisz.'
    },
    {
      title: 'Dokumenty podopiecznego',
      where: 'Podopieczni DOK → Dokumenty',
      link: '/dok-cases',
      steps: [
        'Kliknij „Dokumenty” przy podopiecznym.',
        'Najszybciej: „Prześlij pliki” – każdy plik staje się pozycją na liście.',
        'Albo „Dodaj pozycję” (np. „Metryka chrztu”), a plik dołącz później przyciskiem „Prześlij plik”.',
        'Pozycję można pobrać, zamienić plik, zmienić nazwę albo usunąć.'
      ],
      tip: 'Dozwolone pliki: PDF, JPG, PNG, DOCX, DOC, TXT, do 20 MB.'
    },
    {
      title: 'Spotkania i obecność',
      where: 'Menu → Harmonogram i obecności',
      link: '/meetings',
      steps: [
        'Kliknij „＋ Dodaj spotkanie”. Podaj datę i godziny.',
        'Spotkanie z jedną osobą: wybierz ją w polu „Podopieczny DOK”. Spotkanie grupy: zostaw „spotkanie grupowe”, wpisz opis grupy i zaznacz uczestników.',
        'Stałe spotkania: zaznacz „Powtarzaj co tydzień” i podaj datę końcową.',
        'Po spotkaniu zaznacz przy każdym uczestniku „Obecny” albo „Nieobecny”.',
        'Przełącznik „Lista / Kalendarz” zmienia widok.'
      ]
    },
    {
      title: 'Przekazanie podopiecznego innemu katechiście',
      where: 'Dashboard → „Twoje notatki w przekazanych sprawach”',
      link: '/dashboard',
      steps: [
        'Gdy dyrektor przekaże Twojego podopiecznego innej osobie, sprawa pojawi się na pulpicie w tej karcie.',
        'Możesz udostępnić nowemu katechiście swoje notatki z tej sprawy – albo zostawić je tylko dla siebie.',
        'Udostępnienie da się cofnąć.'
      ]
    },
    {
      title: 'Materiały',
      where: 'Menu → Zasoby dla katechistów',
      link: '/resources',
      steps: ['Znajdziesz tu materiały przygotowane przez dyrektorów i superwizorów. Kliknij nazwę pliku, żeby go pobrać.']
    }
  ],
  director: [
    {
      title: 'Nowa osoba',
      where: 'Menu → Osoby',
      link: '/people',
      steps: [
        'Kliknij „＋ Dodaj osobę” i wpisz dane. E-mail musi być unikalny; przy powtórzonym telefonie portal zapyta, czy zapisać mimo to.',
        'Wiele osób naraz: „Importuj listę” – wklej listę (jedna osoba w wierszu), sprawdź podgląd i zatwierdź.',
        'Kliknięcie w nazwisko otwiera kartę osoby: wszystko, co portal o niej wie, w jednym miejscu.'
      ]
    },
    {
      title: 'Nowy podopieczny DOK i przydział katechisty',
      where: 'Menu → Podopieczni DOK',
      link: '/dok-cases',
      steps: [
        'Kliknij „＋ Nowy podopieczny”: wybierz osobę, ścieżkę i katechistę prowadzącego.',
        '„Edytuj” zmienia etap, katechistę albo mentora. Nowy katechista dostaje e-mail.',
        '„Przerwij” wstrzymuje formację (z datą i powodem), „Wznów” ją przywraca. „Historia” pokazuje, co i kiedy się zmieniało.'
      ],
      tip: 'Katechista zobaczy swoich podopiecznych dopiero, gdy ma konto powiązane ze swoją osobą. Konta zakłada administrator w „Użytkownicy i role”.'
    },
    {
      title: 'Co wymaga uwagi',
      where: 'Menu → Dashboard',
      link: '/dashboard',
      steps: [
        '„Wymaga uwagi” – sprawy, w których etap trwa dłużej niż rok.',
        '„Sprawy DOK z brakującymi dokumentami” i „Misje do odnowienia” – listy rzeczy do załatwienia.',
        'Kafelki z liczbami na górze są linkami do odpowiednich list.'
      ]
    },
    {
      title: 'Kandydaci i zajęcia Szkoły Katechistów',
      where: 'Menu → Kandydaci SKŚP, Zajęcia SKŚP',
      link: '/candidates',
      steps: [
        'Kandydaci SKŚP → „＋ Nowy kandydat”: osoba i rok formacji.',
        'Koniec roku: zaznacz kandydatów i kliknij „Przenieś zaznaczonych do następnego roku”. Kandydaci z III roku kończą formację; przy brakach portal zapyta, czy zakończyć mimo to.',
        'Zajęcia SKŚP → „＋ Dodaj zajęcia” (rok, data, godziny), potem zaznacz obecność.'
      ]
    },
    {
      title: 'Misje kanoniczne i giełda parafii',
      where: 'Menu → Katechiści, Parafie i giełda',
      link: '/missions',
      steps: [
        'Katechiści: „Udziel posłania” osobie po formacji, „Odnów” misję, która się kończy. Dekret dołączysz w „Załączniki”.',
        'Parafie i giełda: „＋ Nowe zapotrzebowanie” parafii, potem „Skieruj” katechistę.',
        'Skierowanie tworzy misję na rok i wysyła e-mail do proboszcza i do katechisty z danymi kontaktowymi drugiej strony.'
      ]
    },
    {
      title: 'Pismo z wzoru',
      where: 'Menu → Dokumenty i pisma',
      link: '/documents',
      steps: [
        'Zakładka „Pisma”: wybierz wzór i osobę, uzupełnij pola, o które portal zapyta.',
        '„Podgląd” pokazuje pismo; „Generuj PDF” albo „Generuj Word” zapisuje je w historii „Wygenerowane pisma”.',
        'Zakładka „Wzory pism”: treść wzorów. Pola w podwójnych klamrach portal wypełnia sam – zielone są poprawne, żółte to pola do wpisania, czerwone to literówka.',
        'Zakładka „Nagłówek i stopka”: logo i dane ośrodka na papierze firmowym.'
      ]
    },
    {
      title: 'Wiadomość do grupy (mailing)',
      where: 'Menu → Mailing',
      link: '/mailing',
      steps: [
        'Kliknij „＋ Nowa kampania”, wpisz temat i treść, wybierz grupę odbiorców. „Pokaż odbiorców” pokaże, do kogo wiadomość pójdzie.',
        '„Zapisz” tworzy szkic. Załączniki dodasz przy szkicu. „Wyślij” rozsyła wiadomość.',
        'Portal wysyła najwyżej 400 wiadomości na dobę – resztę dośle sam następnego dnia.'
      ],
      tip: 'Każda wiadomość ma w stopce link „wypisz się”. Osoby wypisane są pomijane.'
    },
    {
      title: 'Zgłoszenia z formularzy Tally',
      where: 'Menu → Zgłoszenia z formularzy',
      link: '/form-submissions',
      steps: [
        'Nowe odpowiedzi z formularza czekają w zakładce „Do przejrzenia”.',
        'Kliknij „Przejrzyj”: po lewej odpowiedzi, po prawej uporządkowane dane. Popraw je, wybierz „Nowa osoba” albo aktualizację istniejącej i zapisz.',
        'Brakuje zgłoszenia? Kliknij „Pobierz brakujące z Tally”.'
      ]
    },
    {
      title: 'Superwizje, budżet i eksport',
      where: 'Menu → Superwizje, Budżet',
      link: '/supervisions',
      steps: [
        'Superwizje → „＋ Nowa superwizja”: grupa, data, lista obecności, tematyka i wnioski.',
        'Budżet → „＋ Dodaj operację”; do operacji można dołączyć fakturę. „Eksportuj do Excela” pobiera zestawienie za wybrany okres.',
        'Większość list ma przycisk eksportu do Excela w prawym górnym rogu.'
      ]
    }
  ],
  account: [
    {
      title: 'Pierwsze logowanie i hasło',
      where: 'Prawy górny róg → Moje konto',
      link: '/konto/haslo',
      steps: [
        'Konto zakłada administrator i podaje hasło tymczasowe. Przy pierwszym logowaniu portal poprosi o ustawienie własnego hasła – dopiero potem zobaczysz dane.',
        'Hasło zmienisz w dowolnej chwili w „Moje konto” → „Zmiana hasła”.',
        'Nie pamiętasz hasła? Na stronie logowania kliknij „Nie pamiętam hasła” – dostaniesz e-mail z linkiem ważnym 2 godziny.'
      ],
      tip: 'Po 5 błędnych hasłach konto blokuje się na 15 minut.'
    },
    {
      title: 'Weryfikacja dwuetapowa',
      where: 'Moje konto → Weryfikacja dwuetapowa (MFA)',
      link: '/konto/haslo',
      steps: [
        'Zainstaluj w telefonie Microsoft Authenticator albo Google Authenticator.',
        'Kliknij „Włącz weryfikację dwuetapową”, zeskanuj kod QR i wpisz 6-cyfrowy kod z aplikacji.',
        'Zapisz kody zapasowe w bezpiecznym miejscu – przydadzą się, gdy zgubisz telefon.',
        'Od tej pory przy logowaniu, po haśle, podajesz kod z aplikacji.'
      ],
      tip: 'Portal zawiera dane osobowe wielu osób – włącz to, zwłaszcza jeśli widzisz więcej niż własnych podopiecznych. Zgubiony telefon i brak kodów: MFA wyłączy Ci administrator.'
    },
    {
      title: 'Spotkania w kalendarzu telefonu',
      where: 'Moje konto → Kalendarz w telefonie',
      link: '/konto/haslo',
      steps: [
        'Kliknij „Utwórz adres kalendarza”, potem „Kopiuj”.',
        'W Kalendarzu Google (na komputerze): „Inne kalendarze” → „+” → „Z adresu URL” i wklej adres.',
        'Spotkania z portalu pojawią się w telefonie i będą się same odświeżać.'
      ],
      tip: 'Adres działa jak hasło – nikomu go nie wysyłaj. Gdy trafi w niepowołane ręce, kliknij „Utwórz nowy adres”.'
    },
    {
      title: 'Wylogowanie',
      where: 'Prawy górny róg → Wyloguj',
      steps: [
        'Na cudzym albo wspólnym komputerze zawsze kliknij „Wyloguj”.',
        'Po 20 minutach bezczynności portal wyloguje Cię sam.'
      ]
    }
  ]
};
