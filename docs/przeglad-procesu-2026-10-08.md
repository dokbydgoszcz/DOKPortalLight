# Przegląd procesu DOK Portal — luki (2026-10-08)

Trzy niezależne przeloty po kodzie (tylko odczyt): SKŚP → misja → parafia, cykl życia podopiecznego DOK, luki przekrojowe. Poniżej wyniki **zdeduplikowane i uszeregowane**. Znalezisko oznaczone „(zweryfikowane)” sprawdziłem osobno w kodzie; reszta pochodzi z lektury kodu przez przeglądających i wymaga testu przy naprawie. Pozycje „do sprawdzenia” nie są rozstrzygnięte przez sam kod.

## A. Proces się urywa (brak kroku, którego użytkownik potrzebuje)

1. **Nie da się zmienić etapu ani katechisty sprawy DOK w interfejsie** (zweryfikowane). `dok-cases.service.ts` ma tylko lista/utwórz/usuń; `PUT /api/dok-cases/{id}` istnieje, ale frontend go nie woła. Podopieczny zostaje na etapie startowym, lista Absolwentów się nie zasila. Brak też zmiany katechisty/mentora.
2. **Wygasła misja nic nie uruchamia.** Status „wygasa/wygasła” to tylko etykieta; pulpit nie pokazuje misji wygasających ani oczekujących na posłanie, nie ma przypomnienia, nie ma akcji „Odnów”. Lista Katechiści sortuje po dacie końca, więc historia leży na górze.
3. **Usunięcie osoby nie sprawdza zależności** (zgodne we wszystkich trzech raportach). Soft-delete osoby zostawia kandydata, misje, sprawy DOK, funkcję Proboszcz, konto. Przy wymaganej nawigacji z filtrem EF robi INNER JOIN, więc sprawa/pismo znika z list i eksportów, a liczniki pulpitu (bez joinu) nadal ją liczą. Sprawa usuniętego katechisty wypada z przypomnień bez sygnału. Usunięcie parafii zostawia proboszczowi funkcję z nieistniejącą parafią (zapis jego formularza kończy się błędem).
4. **Skierowanie do parafii i misja są luźno związane.** Odpięcie (Unassign) nie cofa misji ani funkcji; zmiana parafii zapotrzebowania nie zmienia misji ani nie ponawia e-maila; powiązanie to porównanie nazwy parafii jako tekstu (zmiana nazwy → duplikat misji); skierowanie dopisuje parafię do dowolnej misji „bez miejsca”, także wygasłej, bez ruszania dat; nie da się zamknąć zapotrzebowania (`Closed` nigdzie nie jest ustawiane).
5. **Katechista jako funkcja a lista Katechiści to dwa niezależne źródła.** Zdjęcie funkcji w edycji osoby przy aktywnej misji; ręczne nadanie funkcji bez misji; zmiana osoby w misji nie zabiera funkcji poprzedniej; usunięcie jedynej misji absolwenta wraca go na listę „przed udzieleniem posługi”.
6. **Pisma nie mają treści.** Wszystkie 11 typów to ten sam układ (tytuł, data, imię, data urodzenia, parafia, uwagi); zaświadczenia o sakramentach bez daty/miejsca/księgi/podpisu, „Klauzula RODO” bez klauzuli. Czeka na wzory od Ciebie; do tego czasu warto oznaczyć „wzór do uzupełnienia”.
7. **Absolwent jest martwym końcem.** Przypomnienia o dokumentach i spotkaniach dalej idą dla absolwentów; lista absolwentów bez akcji/eksportu/linku do sprawy; `CompletedAtUtc` nie jest zerowane po cofnięciu; w UI surowy klucz ścieżki.
8. **Zmiana katechisty prowadzącego nie ma następstw.** Nowy katechista nie widzi notatek poprzednika ani jego zajęć grupowych; brak powiadomienia o przekazaniu.
9. **Dokumenty sprawy**: nie da się usunąć/zmienić nazwy wymaganego dokumentu (a każdy niedostarczony generuje cotygodniowe przypomnienie); brak listy wymaganych dokumentów per ścieżka; stary plik zostaje osierocony po podmianie.
10. **Notatki duszpasterskie bez edycji i usuwania** (sprostowanie/usunięcie danych).

## B. Bezpieczeństwo i dane osobowe

1. **`GET /api/people` (i `/{id}`) dostępne dla każdego zalogowanego** (zweryfikowane): e-mail, telefon, data urodzenia, uwagi wszystkich osób, w tym podopiecznych katechumenatu. Zakres katechisty tego nie obejmuje. Podobnie `name-days/upcoming`, `parishes`. Brak uprawnienia `People.View`; `pageSize` bez limitu, `page<=0` daje 500.
2. **Rola/uprawnienia żyją w tokenie do 8 h, konta nie da się dezaktywować.** Odejście katechisty nie odcina dostępu do jego spraw. Frontend po wygaśnięciu tokenu dalej uważa użytkownika za zalogowanego (brak obsługi 401). `AppUser.PersonId` nie jest czyszczone po usunięciu osoby.
3. **Absolwenci bez zakresu** (`SearchGraduatesAsync` bez `ForScope`): rola z `Graduates.View` bez `ViewAll` widzi wszystkich.
4. **Imieniny idą do wszystkich kont** (także ról bez dostępu do Osób) i ujawniają podopiecznych; e-maile trafiają do logów (`{Email}`) i do Application Insights.
5. **RODO**: tylko soft-delete, brak anonimizacji/eksportu danych osoby/retencji; `PastoralNote` nawet bez soft-delete; PDF-y i pliki w Blob, wpisy audit logu z nazwiskiem nie wygasają.
6. **Audit log** nie obejmuje: pobrań/wgrań dokumentów sprawy, pobrań załączników misji/superwizji/zasobów, generowania i pobrania pism, tworzenia użytkownika, wysyłki kampanii, tworzenia/edycji/usuwania osób, spraw, kandydatów, parafii, skierowań.
7. **Logowanie bez rate-limitu** (blokada cudzych kont, zaśmiecanie audit logu, budzenie darmowej bazy; `/health/ready` publiczny).
8. **Administratorzy**: `Users.Manage` pozwala nadać `Administrator` komukolwiek i odebrać rolę ostatniemu administratorowi.
9. Drobne: upload sprawdza tylko rozszerzenie; token w `localStorage`; brak nagłówków bezpieczeństwa/HSTS w konfiguracji SPA; e-mail osoby unikalny tylko w aplikacji (brak indeksu); uprawnienia domyślne wstawiane tylko do pustej tabeli.

## C. Operacje i niezawodność

1. **Brak runbooka kopii zapasowych/odtwarzania** (PITR bazy, soft-delete/wersjonowanie Blob). `deploy.yml` wdraża po każdym pushu bez bramki CI; migracje wykonują się przy starcie. Kilka migracji jest destrukcyjnych z stratnym `Down` (`ResetDokCaseStages`, `RemoveMissionGrantedPlace`, `ManualFormationYears` zależny od `UtcNow`).
2. **Darmowa baza usypia się**, a `UseSqlServer` bez `EnableRetryOnFailure`/Connect Timeout, `Migrate()` w starcie bez ponowień — pierwszy start po przestoju może się wywrócić.
3. **Przypomnienia**: ciche pomijanie osób bez e-maila (bez licznika/logu), przesunięte spotkanie nie dostaje przypomnienia (`ReminderSentAtUtc` nie jest zerowane), zajęcia grupowe bez przypomnień, warunek „dokładnie jutro” bez okna (opóźniony cron GitHub = przepada); GitHub wyłącza crony po 60 dniach bez aktywności.
4. **Mailing** (część już naprawiona dziś: błąd jednego adresu, ponowna wysyłka): zostaje synchroniczność w jednym żądaniu HTTP (limit ~230 s przy setkach odbiorców), brak statusu „Sending” i blokady współbieżnej po stronie serwera, brak potwierdzenia z liczbą odbiorców, brak wypisu.
5. Kandydaci SKŚP: brak unikalności osoby (duplikaty zawyżają liczniki), możliwość cofnięcia „Ukończył formację” po udzieleniu posłania, zatrzymanie/wznowienie formacji bez śladu i daty.
6. Skierowanie: można skierować dowolną osobę (np. kandydata w formacji); e-mail jest „best effort”, a toast twierdzi, że poszedł; brak informacji, kto dostał; brak e-maila do proboszcza wskazanego później.
7. Listy wyboru osób ładują tylko 200 pierwszych (kandydat, misja, sprawa DOK, skierowanie) — przy większej bazie część osób jest nie do wybrania; brak filtra funkcji Katechista.
8. Brak walidatorów (misje: koniec ≥ początek, wymagane miejsce; sprawy DOK: istnienie osób, funkcja katechisty, unikalność, zakaz zmiany `PersonId`; spotkania: data, obecność w przyszłości; zapotrzebowanie przy tworzeniu).
9. `DokCase.LastMeetingDate` jest martwym polem (eksport zawsze pusty); eksport spotkań grupowych gubi uczestników; eksport spraw bez frekwencji/etapu od kiedy.
10. Zajęcia grupowe założone przez dyrektora nie mają właściciela, więc katechiści uczestników ich nie widzą.
11. Uprawnienia w procesie SKŚP nierówne (może być zamierzone — do potwierdzenia): Dyrektor DOK udziela posłań i edytuje misje, ale nie widzi kandydatów; Biskup widzi oczekujących, a nie widzi kandydatów; skierowanie tworzy misje bez uprawnienia `Missions.Manage`.

## Do sprawdzenia (kod tego nie rozstrzyga)

- Ustawienia Azure poza repo: HTTPS Only, `Reminders__ApiKey`, silny `Jwt__Key`, hasło `SeedAdmin__Password` (czy nie jest domyślne), retencja PITR, soft-delete Blob.
- Czy Application Insights zapisuje pełny URL (wyszukiwarka osób: `?query=nazwisko`), komunikaty 400 z nazwiskami.
- Test integracyjny usuniętej osoby (potwierdzenie INNER JOIN) dla spraw, kandydatów, pism.
- `StageSinceUtc` po migracji dla starych spraw (czy nie wpadają od razu w „etap > rok”); jeden próg „rok” dla wszystkich etapów.
- ClosedXML: przypisywanie `.Value = string` (formuły z pól tekstowych).

## Proponowana kolejność napraw

1. **Spójność danych i usuwanie**: blokada usunięcia osoby/parafii z zależnościami, filtr po `Person` w kandydatach/misjach, spójność misja↔funkcja↔skierowanie (A3–A5).
2. **Proces DOK**: edycja sprawy (etap, katechista, mentor) z historią zmian, pomijanie absolwentów w przypomnieniach, `ForScope` dla absolwentów (A1, A7, B3).
3. **Dostęp i konta**: `People.View` + limity stron, dezaktywacja konta + sprawdzanie ról po stronie serwera + obsługa 401, ochrona ostatniego administratora, imieniny tylko dla właściwych ról (B1, B2, B4, B8).
4. **Misje**: pulpit „misje wygasające”, filtr „aktualne”, „Odnów” (A2).
5. **Operacje**: runbook kopii, bramka CI przed deployem, retry bazy, rate-limit logowania, audit rozszerzony (C1, C2, B6, B7).
6. **Pisma** — po dostarczeniu wzorów (A6).
